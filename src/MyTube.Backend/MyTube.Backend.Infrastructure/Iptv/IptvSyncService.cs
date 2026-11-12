using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application.Iptv;
using MyTube.Backend.Domain;

namespace MyTube.Backend.Infrastructure.Iptv;

public class IptvSyncService : IIptvSyncService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IIptvCatalogClient _catalogClient;
    private readonly ILogger<IptvSyncService> _logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    private const string BaseApiUrl = "https://iptv-org.github.io/api";

    private bool _isSyncing;
    private string _status = "Idle";
    private string? _lastError;
    private DateTime? _lastSyncAt;

    public IptvSyncService(
        IServiceScopeFactory scopeFactory,
        IIptvCatalogClient catalogClient,
        ILogger<IptvSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _catalogClient = catalogClient;
        _logger = logger;
    }

    public async Task<IptvSyncStatusDto> GetSyncStatusAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VideoDbContext>();

        var channelCount = await db.IptvChannels.CountAsync(c => c.IsActive, ct);
        var streamCount = await db.IptvStreams.CountAsync(s => s.IsActive, ct);
        var states = await db.IptvSyncStates
            .OrderByDescending(s => s.LastSyncAt)
            .Select(s => new IptvSyncStateDto
            {
                DatasetName = s.DatasetName,
                LastSyncAt = s.LastSyncAt,
                ItemCount = s.ItemCount,
                Status = s.Status
            })
            .ToListAsync(ct);

        var lastSync = states.FirstOrDefault()?.LastSyncAt ?? _lastSyncAt;

        return new IptvSyncStatusDto
        {
            IsSyncing = _isSyncing,
            Status = _status,
            LastError = _lastError,
            LastSyncAt = lastSync,
            ChannelsCount = channelCount,
            StreamsCount = streamCount,
            States = states
        };
    }

    public async Task<IptvSyncStatusDto> SyncCatalogAsync(bool force = false, CancellationToken ct = default)
    {
        if (!await _syncLock.WaitAsync(0, ct))
        {
            _logger.LogWarning("IPTV synchronization is already running. Skipping concurrent request.");
            return await GetSyncStatusAsync(ct);
        }

        _isSyncing = true;
        _status = "Starting";
        _lastError = null;

        try
        {
            _logger.LogInformation("Starting IPTV catalogue synchronization from {BaseUrl}...", BaseApiUrl);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VideoDbContext>();

            // 1. Load existing sync states
            var syncStates = await db.IptvSyncStates.ToDictionaryAsync(s => s.DatasetName, s => s, ct);

            // 2. Sync Countries
            _status = "Syncing countries";
            await SyncCountriesAsync(db, syncStates, force, ct);

            // 3. Sync Regions
            _status = "Syncing regions";
            await SyncRegionsAsync(db, syncStates, force, ct);

            // 4. Sync Categories
            _status = "Syncing categories";
            await SyncCategoriesAsync(db, syncStates, force, ct);

            // 5. Sync Languages
            _status = "Syncing languages";
            await SyncLanguagesAsync(db, syncStates, force, ct);

            // 6. Fetch Feeds, Logos & Blocklist concurrently
            _status = "Fetching metadata (logos, feeds, blocklist)";
            var feedsTask = _catalogClient.FetchDatasetAsync<List<IptvOrgFeedDto>>($"{BaseApiUrl}/feeds.json", null, null, ct);
            var logosTask = _catalogClient.FetchDatasetAsync<List<IptvOrgLogoDto>>($"{BaseApiUrl}/logos.json", null, null, ct);
            var blocklistTask = _catalogClient.FetchDatasetAsync<List<IptvOrgBlocklistDto>>($"{BaseApiUrl}/blocklist.json", null, null, ct);

            await Task.WhenAll(feedsTask, logosTask, blocklistTask);

            var feeds = feedsTask.Result.Data ?? [];
            var logos = logosTask.Result.Data ?? [];
            var blocklist = blocklistTask.Result.Data ?? [];

            var blockedChannelIds = new HashSet<string>(blocklist.Select(b => b.Channel).Where(c => !string.IsNullOrEmpty(c)), StringComparer.OrdinalIgnoreCase);

            var logoMap = logos
                .Where(l => l.InUse && !string.IsNullOrWhiteSpace(l.Channel) && !string.IsNullOrWhiteSpace(l.Url))
                .GroupBy(l => l.Channel, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Url, StringComparer.OrdinalIgnoreCase);

            var feedLangMap = feeds
                .Where(f => !string.IsNullOrWhiteSpace(f.Channel) && f.Languages != null && f.Languages.Count > 0)
                .GroupBy(f => f.Channel, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Languages!.First(), StringComparer.OrdinalIgnoreCase);

            // 7. Fetch Streams
            _status = "Fetching streams";
            var streamsState = syncStates.GetValueOrDefault("streams");
            var streamsResult = await _catalogClient.FetchDatasetAsync<List<IptvOrgStreamDto>>(
                $"{BaseApiUrl}/streams.json",
                force ? null : streamsState?.ETag,
                force ? null : streamsState?.LastModified,
                ct);

            // 8. Fetch Channels
            _status = "Fetching channels";
            var channelsState = syncStates.GetValueOrDefault("channels");
            var channelsResult = await _catalogClient.FetchDatasetAsync<List<IptvOrgChannelDto>>(
                $"{BaseApiUrl}/channels.json",
                force ? null : channelsState?.ETag,
                force ? null : channelsState?.LastModified,
                ct);

            // If neither streams nor channels changed, and we have existing data, we can finish early
            if (streamsResult.IsNotModified && channelsResult.IsNotModified && !force)
            {
                _logger.LogInformation("Both channels.json and streams.json are unchanged (304 Not Modified). Sync complete.");
                _status = "Completed (Not Modified)";
                _lastSyncAt = DateTime.UtcNow;
                return await GetSyncStatusAsync(ct);
            }

            var streams = streamsResult.Data ?? [];
            var channels = channelsResult.Data ?? [];

            if (streams.Count == 0 && streamsResult.IsNotModified && channels.Count > 0)
            {
                // If streams were 304 but channels were new, load streams fresh
                var freshStreams = await _catalogClient.FetchDatasetAsync<List<IptvOrgStreamDto>>($"{BaseApiUrl}/streams.json", null, null, ct);
                streams = freshStreams.Data ?? [];
            }
            else if (channels.Count == 0 && channelsResult.IsNotModified && streams.Count > 0)
            {
                // If channels were 304 but streams were new, load channels fresh
                var freshChannels = await _catalogClient.FetchDatasetAsync<List<IptvOrgChannelDto>>($"{BaseApiUrl}/channels.json", null, null, ct);
                channels = freshChannels.Data ?? [];
            }

            // 9. Index streams by channel
            var streamsByChannel = streams
                .Where(s => !string.IsNullOrWhiteSpace(s.Channel) && !string.IsNullOrWhiteSpace(s.Url) && !blockedChannelIds.Contains(s.Channel))
                .GroupBy(s => s.Channel!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            // 10. Filter channels to only active, non-NSFW channels that have at least one stream
            var validChannels = channels
                .Where(c => !string.IsNullOrWhiteSpace(c.Id) &&
                            !c.IsNsfw &&
                            string.IsNullOrEmpty(c.Closed) &&
                            !blockedChannelIds.Contains(c.Id) &&
                            streamsByChannel.ContainsKey(c.Id))
                .ToList();

            _logger.LogInformation("Found {Count} active channels with valid streams.", validChannels.Count);

            // 11. Upsert Channels & Streams in chunks
            _status = $"Saving {validChannels.Count} channels to database";
            await UpsertChannelsAndStreamsAsync(db, validChannels, streamsByChannel, logoMap, feedLangMap, ct);

            // 12. Update sync states
            await UpdateSyncStateAsync(db, syncStates, "channels", channelsResult.NewETag, channelsResult.NewLastModified, validChannels.Count, ct);
            await UpdateSyncStateAsync(db, syncStates, "streams", streamsResult.NewETag, streamsResult.NewLastModified, streams.Count, ct);
            await db.SaveChangesAsync(ct);

            _lastSyncAt = DateTime.UtcNow;
            _status = "Completed";
            _logger.LogInformation("IPTV synchronization completed successfully. Synchronized {ChannelCount} channels.", validChannels.Count);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            _status = "Failed";
            _logger.LogError(ex, "IPTV synchronization failed: {Message}", ex.Message);
        }
        finally
        {
            _isSyncing = false;
            _syncLock.Release();
        }

        return await GetSyncStatusAsync(ct);
    }

    private async Task SyncCountriesAsync(
        VideoDbContext db,
        Dictionary<string, IptvSyncState> syncStates,
        bool force,
        CancellationToken ct)
    {
        var state = syncStates.GetValueOrDefault("countries");
        var result = await _catalogClient.FetchDatasetAsync<List<IptvOrgCountryDto>>(
            $"{BaseApiUrl}/countries.json",
            force ? null : state?.ETag,
            force ? null : state?.LastModified,
            ct);

        if (result.IsNotModified || result.Data == null) return;

        var existingCodes = await db.IptvCountries.Select(c => c.Code).ToHashSetAsync(ct);
        var existingEntities = await db.IptvCountries.ToDictionaryAsync(c => c.Code, c => c, ct);

        foreach (var c in result.Data.Where(c => !string.IsNullOrWhiteSpace(c.Code)))
        {
            var langsJson = c.Languages != null && c.Languages.Count > 0 ? JsonSerializer.Serialize(c.Languages) : null;
            if (existingEntities.TryGetValue(c.Code, out var existing))
            {
                existing.Name = c.Name;
                existing.Flag = c.Flag;
                existing.LanguagesJson = langsJson;
            }
            else
            {
                db.IptvCountries.Add(new IptvCountry
                {
                    Code = c.Code,
                    Name = c.Name,
                    Flag = c.Flag,
                    LanguagesJson = langsJson
                });
            }
        }

        await UpdateSyncStateAsync(db, syncStates, "countries", result.NewETag, result.NewLastModified, result.Data.Count, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task SyncRegionsAsync(
        VideoDbContext db,
        Dictionary<string, IptvSyncState> syncStates,
        bool force,
        CancellationToken ct)
    {
        var state = syncStates.GetValueOrDefault("regions");
        var result = await _catalogClient.FetchDatasetAsync<List<IptvOrgRegionDto>>(
            $"{BaseApiUrl}/regions.json",
            force ? null : state?.ETag,
            force ? null : state?.LastModified,
            ct);

        if (result.IsNotModified || result.Data == null) return;

        var existingRegions = await db.IptvRegions.ToDictionaryAsync(r => r.Code, r => r, ct);
        var existingLinks = await db.IptvRegionCountries
            .Select(rc => $"{rc.RegionCode}:{rc.CountryCode}")
            .ToHashSetAsync(ct);

        var validCountryCodes = await db.IptvCountries.Select(c => c.Code).ToHashSetAsync(ct);

        foreach (var r in result.Data.Where(r => !string.IsNullOrWhiteSpace(r.Code)))
        {
            if (existingRegions.TryGetValue(r.Code, out var existing))
            {
                existing.Name = r.Name;
            }
            else
            {
                db.IptvRegions.Add(new IptvRegion { Code = r.Code, Name = r.Name });
            }

            if (r.Countries != null)
            {
                foreach (var countryCode in r.Countries.Where(c => validCountryCodes.Contains(c)))
                {
                    var linkKey = $"{r.Code}:{countryCode}";
                    if (!existingLinks.Contains(linkKey))
                    {
                        db.IptvRegionCountries.Add(new IptvRegionCountry
                        {
                            RegionCode = r.Code,
                            CountryCode = countryCode
                        });
                        existingLinks.Add(linkKey);
                    }
                }
            }
        }

        await UpdateSyncStateAsync(db, syncStates, "regions", result.NewETag, result.NewLastModified, result.Data.Count, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task SyncCategoriesAsync(
        VideoDbContext db,
        Dictionary<string, IptvSyncState> syncStates,
        bool force,
        CancellationToken ct)
    {
        var state = syncStates.GetValueOrDefault("categories");
        var result = await _catalogClient.FetchDatasetAsync<List<IptvOrgCategoryDto>>(
            $"{BaseApiUrl}/categories.json",
            force ? null : state?.ETag,
            force ? null : state?.LastModified,
            ct);

        if (result.IsNotModified || result.Data == null) return;

        var existing = await db.IptvCategories.ToDictionaryAsync(c => c.Id, c => c, ct);
        foreach (var cat in result.Data.Where(c => !string.IsNullOrWhiteSpace(c.Id)))
        {
            if (existing.TryGetValue(cat.Id, out var existingCat))
            {
                existingCat.Name = cat.Name;
                existingCat.Description = cat.Description;
            }
            else
            {
                db.IptvCategories.Add(new IptvCategory
                {
                    Id = cat.Id,
                    Name = cat.Name,
                    Description = cat.Description
                });
            }
        }

        await UpdateSyncStateAsync(db, syncStates, "categories", result.NewETag, result.NewLastModified, result.Data.Count, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task SyncLanguagesAsync(
        VideoDbContext db,
        Dictionary<string, IptvSyncState> syncStates,
        bool force,
        CancellationToken ct)
    {
        var state = syncStates.GetValueOrDefault("languages");
        var result = await _catalogClient.FetchDatasetAsync<List<IptvOrgLanguageDto>>(
            $"{BaseApiUrl}/languages.json",
            force ? null : state?.ETag,
            force ? null : state?.LastModified,
            ct);

        if (result.IsNotModified || result.Data == null) return;

        var existing = await db.IptvLanguages.ToDictionaryAsync(l => l.Code, l => l, ct);
        foreach (var lang in result.Data.Where(l => !string.IsNullOrWhiteSpace(l.Code)))
        {
            if (existing.TryGetValue(lang.Code, out var existingLang))
            {
                existingLang.Name = lang.Name;
            }
            else
            {
                db.IptvLanguages.Add(new IptvLanguage
                {
                    Code = lang.Code,
                    Name = lang.Name
                });
            }
        }

        await UpdateSyncStateAsync(db, syncStates, "languages", result.NewETag, result.NewLastModified, result.Data.Count, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task UpsertChannelsAndStreamsAsync(
        VideoDbContext db,
        List<IptvOrgChannelDto> channels,
        Dictionary<string, List<IptvOrgStreamDto>> streamsByChannel,
        Dictionary<string, string> logoMap,
        Dictionary<string, string> feedLangMap,
        CancellationToken ct)
    {
        var validCountryCodes = await db.IptvCountries.Select(c => c.Code).ToHashSetAsync(ct);
        var validLanguageCodes = await db.IptvLanguages.Select(l => l.Code).ToHashSetAsync(ct);
        var validCategoryIds = await db.IptvCategories.Select(c => c.Id).ToHashSetAsync(ct);

        const int chunkSize = 500;
        for (var i = 0; i < channels.Count; i += chunkSize)
        {
            var chunk = channels.Skip(i).Take(chunkSize).ToList();
            var chunkIds = chunk.Select(c => c.Id).ToList();

            var existingChannels = await db.IptvChannels
                .Include(c => c.Streams)
                .Include(c => c.ChannelCategories)
                .Where(c => chunkIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c, ct);

            foreach (var ch in chunk)
            {
                var countryCode = !string.IsNullOrEmpty(ch.Country) && ch.Country.Length <= 10 && validCountryCodes.Contains(ch.Country) ? ch.Country : null;
                var langCode = feedLangMap.TryGetValue(ch.Id, out var fl) && fl.Length <= 10 && validLanguageCodes.Contains(fl) ? fl : null;
                var logoUrl = logoMap.GetValueOrDefault(ch.Id);
                var altNames = ch.AltNames != null && ch.AltNames.Count > 0 ? string.Join(", ", ch.AltNames) : null;
                var channelName = string.IsNullOrWhiteSpace(ch.Name) ? ch.Id : (ch.Name.Length > 255 ? ch.Name[..255] : ch.Name);

                if (!existingChannels.TryGetValue(ch.Id, out var channelEntity))
                {
                    channelEntity = new IptvChannel
                    {
                        Id = ch.Id.Length > 128 ? ch.Id[..128] : ch.Id,
                        Name = channelName,
                        AltNames = altNames,
                        Network = ch.Network,
                        CountryCode = countryCode,
                        Subdivision = ch.Subdivision,
                        City = ch.City,
                        Website = ch.Website,
                        LogoUrl = logoUrl,
                        PrimaryLanguageCode = langCode,
                        IsNsfw = ch.IsNsfw,
                        IsClosed = !string.IsNullOrEmpty(ch.Closed),
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    db.IptvChannels.Add(channelEntity);
                }
                else
                {
                    channelEntity.Name = channelName;
                    channelEntity.AltNames = altNames;
                    channelEntity.Network = ch.Network;
                    channelEntity.CountryCode = countryCode;
                    channelEntity.Subdivision = ch.Subdivision;
                    channelEntity.City = ch.City;
                    channelEntity.Website = ch.Website;
                    if (!string.IsNullOrEmpty(logoUrl)) channelEntity.LogoUrl = logoUrl;
                    if (!string.IsNullOrEmpty(langCode)) channelEntity.PrimaryLanguageCode = langCode;
                    channelEntity.IsActive = true;
                    channelEntity.UpdatedAt = DateTime.UtcNow;
                }

                // Sync Categories for this channel
                if (ch.Categories != null && ch.Categories.Count > 0)
                {
                    var desiredCategories = ch.Categories.Where(cat => validCategoryIds.Contains(cat)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var existingCats = channelEntity.ChannelCategories.Select(cc => cc.CategoryId).ToHashSet(StringComparer.OrdinalIgnoreCase);

                    foreach (var catId in desiredCategories)
                    {
                        if (!existingCats.Contains(catId))
                        {
                            channelEntity.ChannelCategories.Add(new IptvChannelCategory
                            {
                                ChannelId = channelEntity.Id,
                                CategoryId = catId
                            });
                        }
                    }
                }

                // Sync Streams for this channel
                if (streamsByChannel.TryGetValue(ch.Id, out var streamDtos))
                {
                    var existingStreamUrls = channelEntity.Streams.ToDictionary(s => s.Url, s => s, StringComparer.OrdinalIgnoreCase);

                    var orderIdx = 0;
                    foreach (var sDto in streamDtos)
                    {
                        if (string.IsNullOrWhiteSpace(sDto.Url)) continue;

                        var feed = sDto.Feed != null && sDto.Feed.Length > 100 ? sDto.Feed[..100] : sDto.Feed;
                        var quality = sDto.Quality != null && sDto.Quality.Length > 50 ? sDto.Quality[..50] : sDto.Quality;
                        var labels = sDto.Labels != null ? string.Join(", ", sDto.Labels) : null;

                        if (existingStreamUrls.TryGetValue(sDto.Url, out var existingStream))
                        {
                            existingStream.Feed = feed;
                            existingStream.Title = sDto.Title;
                            existingStream.Quality = quality;
                            existingStream.UserAgent = sDto.UserAgent;
                            existingStream.Referrer = sDto.Referrer;
                            existingStream.Labels = labels;
                            existingStream.OrderIndex = orderIdx++;
                            existingStream.IsActive = true;
                        }
                        else
                        {
                            channelEntity.Streams.Add(new IptvStream
                            {
                                ChannelId = channelEntity.Id,
                                Feed = feed,
                                Title = sDto.Title,
                                Url = sDto.Url,
                                Quality = quality,
                                UserAgent = sDto.UserAgent,
                                Referrer = sDto.Referrer,
                                Labels = labels,
                                OrderIndex = orderIdx++,
                                IsActive = true,
                                Status = "Unknown"
                            });
                        }
                    }
                }
            }

            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task UpdateSyncStateAsync(
        VideoDbContext db,
        Dictionary<string, IptvSyncState> syncStates,
        string datasetName,
        string? etag,
        DateTime? lastModified,
        int itemCount,
        CancellationToken ct)
    {
        if (syncStates.TryGetValue(datasetName, out var state))
        {
            state.ETag = etag;
            state.LastModified = lastModified;
            state.LastSyncAt = DateTime.UtcNow;
            state.ItemCount = itemCount;
            state.Status = "Success";
            state.ErrorMessage = null;
        }
        else
        {
            var newState = new IptvSyncState
            {
                DatasetName = datasetName,
                ETag = etag,
                LastModified = lastModified,
                LastSyncAt = DateTime.UtcNow,
                ItemCount = itemCount,
                Status = "Success"
            };
            db.IptvSyncStates.Add(newState);
            syncStates[datasetName] = newState;
        }
    }
}

