using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application;
using MyTube.Backend.Application.Iptv;
using MyTube.Backend.Domain;

namespace MyTube.Backend.Infrastructure.Iptv;

public class IptvService : IIptvService
{
    private readonly VideoDbContext _db;
    private readonly ILogger<IptvService> _logger;

    public IptvService(VideoDbContext db, ILogger<IptvService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PagedResult<IptvChannelDto>> GetChannelsAsync(
        string? userId,
        string? scope,
        string? regionCode,
        string? countryCode,
        string? languageCode,
        string? categoryId,
        string? search,
        bool favoritesOnly,
        bool historyOnly,
        int page,
        int pageSize,
        bool httpsOnly = false,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.IptvChannels
            .AsNoTracking()
            .Where(c => c.IsActive && !c.IsClosed && c.Streams.Any(s => s.IsActive));

        // 1. History Filter
        if (historyOnly && !string.IsNullOrWhiteSpace(userId))
        {
            var historyChannelIds = await _db.IptvWatchHistories
                .AsNoTracking()
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.WatchedAt)
                .Select(h => h.ChannelId)
                .Distinct()
                .ToListAsync(ct);

            query = query.Where(c => historyChannelIds.Contains(c.Id));
        }

        // 2. Favorites Filter
        if (favoritesOnly && !string.IsNullOrWhiteSpace(userId))
        {
            var favChannelIds = await _db.IptvFavorites
                .AsNoTracking()
                .Where(f => f.UserId == userId)
                .Select(f => f.ChannelId)
                .ToListAsync(ct);

            query = query.Where(c => favChannelIds.Contains(c.Id));
        }

        // 3. Taxonomy Scope Filters
        var normalizedScope = scope?.ToLowerInvariant() ?? "worldwide";
        switch (normalizedScope)
        {
            case "region" when !string.IsNullOrWhiteSpace(regionCode):
                var regionCountryCodes = await _db.IptvRegionCountries
                    .AsNoTracking()
                    .Where(rc => rc.RegionCode.ToLower() == regionCode.ToLower())
                    .Select(rc => rc.CountryCode)
                    .ToListAsync(ct);
                query = query.Where(c => c.CountryCode != null && regionCountryCodes.Contains(c.CountryCode));
                break;

            case "country" when !string.IsNullOrWhiteSpace(countryCode):
                query = query.Where(c => c.CountryCode != null && c.CountryCode.ToLower() == countryCode.ToLower());
                break;

            case "language" when !string.IsNullOrWhiteSpace(languageCode):
                query = query.Where(c => c.PrimaryLanguageCode != null && c.PrimaryLanguageCode.ToLower() == languageCode.ToLower());
                break;

            case "category" when !string.IsNullOrWhiteSpace(categoryId):
                query = query.Where(c => c.ChannelCategories.Any(cc => cc.CategoryId.ToLower() == categoryId.ToLower()));
                break;
        }

        // 4. Secondary filters (can combine with Scope)
        if (!string.IsNullOrWhiteSpace(countryCode) && normalizedScope != "country")
        {
            query = query.Where(c => c.CountryCode != null && c.CountryCode.ToLower() == countryCode.ToLower());
        }

        if (!string.IsNullOrWhiteSpace(categoryId) && normalizedScope != "category")
        {
            query = query.Where(c => c.ChannelCategories.Any(cc => cc.CategoryId.ToLower() == categoryId.ToLower()));
        }

        if (!string.IsNullOrWhiteSpace(languageCode) && normalizedScope != "language")
        {
            query = query.Where(c => c.PrimaryLanguageCode != null && c.PrimaryLanguageCode.ToLower() == languageCode.ToLower());
        }

        // 5. Search
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(term) ||
                (c.AltNames != null && c.AltNames.ToLower().Contains(term)) ||
                (c.Network != null && c.Network.ToLower().Contains(term)));
        }

        // 6. HTTPS Only (vital for HTTPS deployments to prevent mixed-content blocks)
        if (httpsOnly)
        {
            query = query.Where(c => c.Streams.Any(s => s.IsActive && s.Url.StartsWith("https://")));
        }

        var totalCount = await query.CountAsync(ct);

        var pagedEntities = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.AltNames,
                c.Network,
                c.CountryCode,
                CountryName = c.Country != null ? c.Country.Name : null,
                CountryFlag = c.Country != null ? c.Country.Flag : null,
                c.PrimaryLanguageCode,
                PrimaryLanguageName = c.PrimaryLanguage != null ? c.PrimaryLanguage.Name : null,
                c.Website,
                c.LogoUrl,
                Categories = c.ChannelCategories.Select(cc => cc.Category!.Name).ToList(),
                Streams = c.Streams
                    .Where(s => s.IsActive)
                    .OrderByDescending(s => s.Url.StartsWith("https://"))
                    .ThenBy(s => s.OrderIndex)
                    .Select(s => new IptvStreamDto
                    {
                        Id = s.Id,
                        Feed = s.Feed,
                        Title = s.Title,
                        Url = s.Url,
                        Quality = s.Quality,
                        UserAgent = s.UserAgent,
                        Referrer = s.Referrer,
                        Labels = s.Labels,
                        Status = s.Status,
                        OrderIndex = s.OrderIndex
                    })
                    .ToList()
            })
            .ToListAsync(ct);

        // Compute Favorite status & Last Watched for current user
        var channelIds = pagedEntities.Select(e => e.Id).ToList();
        var userFavSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var userWatchDict = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var favList = await _db.IptvFavorites
                .AsNoTracking()
                .Where(f => f.UserId == userId && channelIds.Contains(f.ChannelId))
                .Select(f => f.ChannelId)
                .ToListAsync(ct);
            userFavSet = new HashSet<string>(favList, StringComparer.OrdinalIgnoreCase);

            var watchList = await _db.IptvWatchHistories
                .AsNoTracking()
                .Where(h => h.UserId == userId && channelIds.Contains(h.ChannelId))
                .GroupBy(h => h.ChannelId)
                .Select(g => new { ChannelId = g.Key, WatchedAt = g.Max(h => h.WatchedAt) })
                .ToListAsync(ct);
            userWatchDict = watchList.ToDictionary(w => w.ChannelId, w => w.WatchedAt, StringComparer.OrdinalIgnoreCase);
        }

        var items = pagedEntities.Select(e => new IptvChannelDto
        {
            Id = e.Id,
            Name = e.Name,
            AltNames = e.AltNames,
            Network = e.Network,
            CountryCode = e.CountryCode,
            CountryName = e.CountryName,
            CountryFlag = e.CountryFlag,
            PrimaryLanguageCode = e.PrimaryLanguageCode,
            PrimaryLanguageName = e.PrimaryLanguageName,
            Website = e.Website,
            LogoUrl = e.LogoUrl,
            Categories = e.Categories,
            Streams = e.Streams,
            IsFavorite = userFavSet.Contains(e.Id),
            LastWatchedAt = userWatchDict.GetValueOrDefault(e.Id)
        }).ToList();

        return new PagedResult<IptvChannelDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IptvChannelDto?> GetChannelByIdAsync(string channelId, string? userId, CancellationToken ct = default)
    {
        var channel = await _db.IptvChannels
            .AsNoTracking()
            .Include(c => c.Country)
            .Include(c => c.PrimaryLanguage)
            .Include(c => c.ChannelCategories).ThenInclude(cc => cc.Category)
            .Include(c => c.Streams)
            .FirstOrDefaultAsync(c => c.Id == channelId && c.IsActive, ct);

        if (channel == null) return null;

        var isFav = false;
        DateTime? lastWatched = null;

        if (!string.IsNullOrWhiteSpace(userId))
        {
            isFav = await _db.IptvFavorites.AnyAsync(f => f.UserId == userId && f.ChannelId == channelId, ct);
            var history = await _db.IptvWatchHistories
                .Where(h => h.UserId == userId && h.ChannelId == channelId)
                .OrderByDescending(h => h.WatchedAt)
                .FirstOrDefaultAsync(ct);
            lastWatched = history?.WatchedAt;
        }

        return new IptvChannelDto
        {
            Id = channel.Id,
            Name = channel.Name,
            AltNames = channel.AltNames,
            Network = channel.Network,
            CountryCode = channel.CountryCode,
            CountryName = channel.Country?.Name,
            CountryFlag = channel.Country?.Flag,
            PrimaryLanguageCode = channel.PrimaryLanguageCode,
            PrimaryLanguageName = channel.PrimaryLanguage?.Name,
            Website = channel.Website,
            LogoUrl = channel.LogoUrl,
            IsFavorite = isFav,
            LastWatchedAt = lastWatched,
            Categories = channel.ChannelCategories.Select(cc => cc.Category!.Name).ToList(),
            Streams = channel.Streams
                .Where(s => s.IsActive)
                .OrderBy(s => s.OrderIndex)
                .Select(s => new IptvStreamDto
                {
                    Id = s.Id,
                    Feed = s.Feed,
                    Title = s.Title,
                    Url = s.Url,
                    Quality = s.Quality,
                    UserAgent = s.UserAgent,
                    Referrer = s.Referrer,
                    Labels = s.Labels,
                    Status = s.Status,
                    OrderIndex = s.OrderIndex
                })
                .ToList()
        };
    }

    public async Task<IptvFilterOptionsDto> GetFilterOptionsAsync(CancellationToken ct = default)
    {
        var activeChannelsQuery = _db.IptvChannels
            .AsNoTracking()
            .Where(c => c.IsActive && !c.IsClosed && c.Streams.Any(s => s.IsActive));

        var totalChannels = await activeChannelsQuery.CountAsync(ct);
        var totalStreams = await _db.IptvStreams.CountAsync(s => s.IsActive && s.Channel!.IsActive, ct);

        // Countries with active channel counts
        var countries = await _db.IptvCountries
            .AsNoTracking()
            .Where(c => c.Channels.Any(ch => ch.IsActive && !ch.IsClosed && ch.Streams.Any(s => s.IsActive)))
            .Select(c => new IptvCountryFilterDto
            {
                Code = c.Code,
                Name = c.Name,
                Flag = c.Flag,
                ChannelCount = c.Channels.Count(ch => ch.IsActive && !ch.IsClosed && ch.Streams.Any(s => s.IsActive))
            })
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        // Regions with active channel counts
        var regions = await _db.IptvRegions
            .AsNoTracking()
            .Select(r => new IptvRegionFilterDto
            {
                Code = r.Code,
                Name = r.Name,
                ChannelCount = r.RegionCountries.SelectMany(rc => rc.Country!.Channels)
                    .Count(ch => ch.IsActive && !ch.IsClosed && ch.Streams.Any(s => s.IsActive))
            })
            .Where(r => r.ChannelCount > 0)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

        // Categories with active channel counts
        var categories = await _db.IptvCategories
            .AsNoTracking()
            .Where(cat => cat.ChannelCategories.Any(cc => cc.Channel!.IsActive && !cc.Channel.IsClosed && cc.Channel.Streams.Any(s => s.IsActive)))
            .Select(cat => new IptvCategoryFilterDto
            {
                Id = cat.Id,
                Name = cat.Name,
                ChannelCount = cat.ChannelCategories.Count(cc => cc.Channel!.IsActive && !cc.Channel.IsClosed && cc.Channel.Streams.Any(s => s.IsActive))
            })
            .OrderBy(cat => cat.Name)
            .ToListAsync(ct);

        // Languages with active channel counts
        var languages = await _db.IptvLanguages
            .AsNoTracking()
            .Where(l => l.Channels.Any(ch => ch.IsActive && !ch.IsClosed && ch.Streams.Any(s => s.IsActive)))
            .Select(l => new IptvLanguageFilterDto
            {
                Code = l.Code,
                Name = l.Name,
                ChannelCount = l.Channels.Count(ch => ch.IsActive && !ch.IsClosed && ch.Streams.Any(s => s.IsActive))
            })
            .OrderBy(l => l.Name)
            .ToListAsync(ct);

        return new IptvFilterOptionsDto
        {
            TotalChannels = totalChannels,
            TotalStreams = totalStreams,
            Regions = regions,
            Countries = countries,
            Categories = categories,
            Languages = languages
        };
    }

    public async Task<bool> ToggleFavoriteAsync(string userId, string channelId, CancellationToken ct = default)
    {
        var existing = await _db.IptvFavorites
            .FirstOrDefaultAsync(f => f.UserId == userId && f.ChannelId == channelId, ct);

        if (existing != null)
        {
            _db.IptvFavorites.Remove(existing);
            await _db.SaveChangesAsync(ct);
            return false;
        }

        var channelExists = await _db.IptvChannels.AnyAsync(c => c.Id == channelId, ct);
        if (!channelExists)
        {
            throw new KeyNotFoundException($"Channel '{channelId}' does not exist.");
        }

        _db.IptvFavorites.Add(new IptvFavorite
        {
            UserId = userId,
            ChannelId = channelId,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<string>> GetUserFavoriteChannelIdsAsync(string userId, CancellationToken ct = default)
    {
        return await _db.IptvFavorites
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .Select(f => f.ChannelId)
            .ToListAsync(ct);
    }

    public async Task RecordWatchHistoryAsync(string userId, string channelId, int? streamId, CancellationToken ct = default)
    {
        var existing = await _db.IptvWatchHistories
            .FirstOrDefaultAsync(h => h.UserId == userId && h.ChannelId == channelId, ct);

        if (existing != null)
        {
            existing.WatchedAt = DateTime.UtcNow;
            if (streamId.HasValue) existing.LastStreamId = streamId;
        }
        else
        {
            _db.IptvWatchHistories.Add(new IptvWatchHistory
            {
                UserId = userId,
                ChannelId = channelId,
                LastStreamId = streamId,
                WatchedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> ReportStreamHealthAsync(int streamId, bool isWorking, string? errorMessage, CancellationToken ct = default)
    {
        var stream = await _db.IptvStreams.FindAsync([streamId], ct);
        if (stream == null) return false;

        stream.LastCheckedAt = DateTime.UtcNow;
        stream.Status = isWorking ? "Online" : "Offline";

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Updated stream {StreamId} health status: {Status} (working={IsWorking})", streamId, stream.Status, isWorking);
        return true;
    }

    public async Task<string> GenerateM3uPlaylistAsync(
        string? userId,
        bool favoritesOnly,
        string? countryCode,
        string? categoryId,
        string? languageCode,
        bool httpsOnly = false,
        CancellationToken ct = default)
    {
        var query = _db.IptvChannels
            .AsNoTracking()
            .Where(c => c.IsActive && !c.IsClosed && c.Streams.Any(s => s.IsActive));

        if (favoritesOnly && !string.IsNullOrWhiteSpace(userId))
        {
            var favChannelIds = await _db.IptvFavorites
                .AsNoTracking()
                .Where(f => f.UserId == userId)
                .Select(f => f.ChannelId)
                .ToListAsync(ct);

            query = query.Where(c => favChannelIds.Contains(c.Id));
        }

        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            query = query.Where(c => c.CountryCode != null && c.CountryCode.ToLower() == countryCode.ToLower());
        }

        if (!string.IsNullOrWhiteSpace(categoryId))
        {
            query = query.Where(c => c.ChannelCategories.Any(cc => cc.CategoryId.ToLower() == categoryId.ToLower()));
        }

        if (!string.IsNullOrWhiteSpace(languageCode))
        {
            query = query.Where(c => c.PrimaryLanguageCode != null && c.PrimaryLanguageCode.ToLower() == languageCode.ToLower());
        }

        if (httpsOnly)
        {
            query = query.Where(c => c.Streams.Any(s => s.IsActive && s.Url.StartsWith("https://")));
        }

        var channels = await query
            .OrderBy(c => c.Name)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.LogoUrl,
                c.CountryCode,
                PrimaryLanguage = c.PrimaryLanguage != null ? c.PrimaryLanguage.Name : null,
                Categories = c.ChannelCategories.Select(cc => cc.Category!.Name).ToList(),
                Streams = c.Streams
                    .Where(s => s.IsActive)
                    .OrderByDescending(s => s.Url.StartsWith("https://"))
                    .ThenBy(s => s.OrderIndex)
                    .Select(s => new { s.Url, s.Quality, s.Title, s.Feed })
                    .ToList()
            })
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("#EXTM3U");

        foreach (var c in channels)
        {
            var category = c.Categories.FirstOrDefault() ?? "General";
            var lang = c.PrimaryLanguage ?? "";
            var country = c.CountryCode ?? "";
            var logo = c.LogoUrl ?? "";

            int streamIndex = 1;
            foreach (var stream in c.Streams)
            {
                if (httpsOnly && !stream.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    continue;

                var streamLabel = c.Streams.Count > 1
                    ? $" ({stream.Quality ?? stream.Title ?? $"Feed {streamIndex}"})"
                    : "";

                // Standard Jellyfin / IPTV EXTINF metadata format
                sb.AppendLine($"#EXTINF:-1 tvg-id=\"{c.Id}\" tvg-name=\"{c.Name}\" tvg-logo=\"{logo}\" tvg-country=\"{country}\" tvg-language=\"{lang}\" group-title=\"{category}\",{c.Name}{streamLabel}");
                sb.AppendLine(stream.Url);

                streamIndex++;
            }
        }

        return sb.ToString();
    }
}
