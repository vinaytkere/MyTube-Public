using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application.Sonarr;
using MyTube.Backend.Domain;
using MyTube.Backend.Infrastructure.Servarr;

namespace MyTube.Backend.Infrastructure.Sonarr;

public class SonarrService : ISonarrService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SonarrService> _logger;
    private SonarrSettings _settings;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public SonarrService(
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<SonarrService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;

        _settings = new SonarrSettings
        {
            BaseUrl = configuration["Sonarr:BaseUrl"] ?? "http://sonarr:8989",
            ApiKey = configuration["Sonarr:ApiKey"] ?? string.Empty,
            RootFolderPath = configuration["Sonarr:RootFolderPath"] ?? "/tv",
            QualityProfileId = int.TryParse(configuration["Sonarr:QualityProfileId"], out var qp) ? qp : 1
        };

        EnsureApiKeyLoaded();
    }

    private void EnsureApiKeyLoaded(bool force = false)
    {
        if (!force && !string.IsNullOrWhiteSpace(_settings.ApiKey)) return;

        var candidates = new[]
        {
            "/sonarr-config/config.xml",
            "/config/config.xml",
            Path.Combine(AppContext.BaseDirectory, "sonarr-config", "config.xml")
        };

        foreach (var path in candidates)
        {
            try
            {
                if (File.Exists(path))
                {
                    var xml = File.ReadAllText(path);
                    var match = Regex.Match(xml, @"<ApiKey>(.*?)</ApiKey>", RegexOptions.IgnoreCase);
                    if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
                    {
                        var key = match.Groups[1].Value.Trim().Trim('"', '\'');
                        _settings.ApiKey = key;
                        _logger.LogInformation("Auto-detected Sonarr API key from {Path}", path);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not read Sonarr config from {Path}", path);
            }
        }
    }

    private async Task<string?> ProbeHealthyBaseUrlAsync(string apiKey, CancellationToken cancellationToken)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(_settings.BaseUrl))
            candidates.Add(_settings.BaseUrl);

        candidates.Add("http://sonarr:8989");
        candidates.Add("http://172.18.0.4:8989");
        candidates.Add("http://172.17.0.1:8989");
        candidates.Add("http://localhost:8989");
        candidates.Add("http://127.0.0.1:8989");
        candidates.Add("http://host.docker.internal:8989");
        candidates.Add("http://192.168.1.16:8989");

        var distinct = candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var cleanKey = apiKey?.Trim().Trim('"', '\'');

        foreach (var candidate in distinct)
        {
            try
            {
                var cleanUrl = candidate.Trim().TrimEnd('/');
                using var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(2);
                try { client.DefaultRequestHeaders.Host = "localhost"; } catch { }
                if (!string.IsNullOrWhiteSpace(cleanKey))
                {
                    client.DefaultRequestHeaders.Add("X-Api-Key", cleanKey);
                }

                var resp = await client.GetAsync($"{cleanUrl}/api/v3/system/status", cancellationToken);
                if (resp.IsSuccessStatusCode || resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    if (!string.Equals(_settings.BaseUrl, cleanUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Automatically recovered Sonarr connection by switching BaseUrl from {Old} to {New}", _settings.BaseUrl, cleanUrl);
                        _settings.BaseUrl = cleanUrl;
                    }
                    return cleanUrl;
                }
            }
            catch
            {
                // Continue probing
            }
        }

        return null;
    }

    public SonarrSettings GetCurrentSettings()
    {
        EnsureApiKeyLoaded();
        return _settings;
    }

    public async Task UpdateSettingsAsync(SonarrSettings newSettings, CancellationToken cancellationToken = default)
    {
        _settings.BaseUrl = (newSettings.BaseUrl ?? "http://sonarr:8989").TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(newSettings.ApiKey) && !newSettings.ApiKey.Contains("****"))
        {
            _settings.ApiKey = newSettings.ApiKey.Trim();
        }
        _settings.RootFolderPath = newSettings.RootFolderPath?.Trim();
        _settings.QualityProfileId = newSettings.QualityProfileId > 0 ? newSettings.QualityProfileId : 1;

        try
        {
            var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(appSettingsPath))
            {
                var json = await File.ReadAllTextAsync(appSettingsPath, cancellationToken);
                var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json, _jsonOptions) ?? new();
                dict["Sonarr"] = new
                {
                    BaseUrl = _settings.BaseUrl,
                    ApiKey = _settings.ApiKey,
                    RootFolderPath = _settings.RootFolderPath,
                    QualityProfileId = _settings.QualityProfileId
                };
                await File.WriteAllTextAsync(appSettingsPath, JsonSerializer.Serialize(dict, _jsonOptions), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist Sonarr settings to appsettings.json. Settings kept in-memory.");
        }
    }

    private HttpClient CreateClient(string? customBaseUrl = null, string? customApiKey = null, TimeSpan? timeout = null)
    {
        EnsureApiKeyLoaded();
        var client = _httpClientFactory.CreateClient();
        ServarrClientHelper.ConfigureClient(client, customBaseUrl ?? _settings.BaseUrl, customApiKey ?? _settings.ApiKey, timeout ?? TimeSpan.FromSeconds(15));
        try { client.DefaultRequestHeaders.Host = "localhost"; } catch { }
        return client;
    }

    public async Task<SonarrStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        EnsureApiKeyLoaded();

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            return new SonarrStatusDto
            {
                IsConfigured = false,
                IsConnected = false,
                ErrorMessage = "Sonarr API key is not configured. Please paste your API key in Settings -> Sonarr."
            };
        }

        var status = await TestConnectionAsync(_settings.BaseUrl, _settings.ApiKey, cancellationToken);
        if (!status.IsConnected)
        {
            var recoveredUrl = await ProbeHealthyBaseUrlAsync(_settings.ApiKey, cancellationToken);
            if (!string.IsNullOrWhiteSpace(recoveredUrl) && !string.Equals(recoveredUrl, _settings.BaseUrl, StringComparison.OrdinalIgnoreCase))
            {
                status = await TestConnectionAsync(recoveredUrl, _settings.ApiKey, cancellationToken);
            }
        }

        return status;
    }

    public async Task<SonarrStatusDto> TestConnectionAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default)
    {
        EnsureApiKeyLoaded();

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = _settings.BaseUrl;
        }

        var effectiveApiKey = apiKey;
        if (string.IsNullOrWhiteSpace(effectiveApiKey) || effectiveApiKey.Contains("****"))
        {
            effectiveApiKey = _settings.ApiKey;
        }

        if (string.IsNullOrWhiteSpace(effectiveApiKey))
        {
            EnsureApiKeyLoaded(force: true);
            effectiveApiKey = _settings.ApiKey;
        }

        if (string.IsNullOrWhiteSpace(effectiveApiKey))
        {
            return new SonarrStatusDto
            {
                IsConfigured = false,
                IsConnected = false,
                ErrorMessage = "Sonarr API key is required. Copy it from Sonarr -> Settings -> General -> Security."
            };
        }

        var cleanBase = (baseUrl ?? "http://sonarr:8989").Trim().TrimEnd('/');
        var cleanKey = effectiveApiKey.Trim().Trim('"', '\'');

        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(8);
            try { client.DefaultRequestHeaders.Host = "localhost"; } catch { }
            client.DefaultRequestHeaders.Add("X-Api-Key", cleanKey);

            var response = await client.GetAsync($"{cleanBase}/api/v3/system/status", cancellationToken);

            // If 400 or 401, check if disk config has a different key, query param works, or Host header override
            if (!response.IsSuccessStatusCode && (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.BadRequest))
            {
                _logger.LogWarning("Sonarr returned {StatusCode} on initial test at {Url}. Attempting disk reload, query param fallback, and Host header override...", response.StatusCode, cleanBase);
                
                // 1. Try disk key reload
                EnsureApiKeyLoaded(force: true);
                if (!string.IsNullOrWhiteSpace(_settings.ApiKey) && !string.Equals(_settings.ApiKey, cleanKey, StringComparison.OrdinalIgnoreCase))
                {
                    cleanKey = _settings.ApiKey.Trim().Trim('"', '\'');
                    using var diskClient = _httpClientFactory.CreateClient();
                    diskClient.Timeout = TimeSpan.FromSeconds(8);
                    try { diskClient.DefaultRequestHeaders.Host = "localhost"; } catch { }
                    diskClient.DefaultRequestHeaders.Add("X-Api-Key", cleanKey);
                    response = await diskClient.GetAsync($"{cleanBase}/api/v3/system/status", cancellationToken);
                }

                // 2. If still not successful, try ?apikey= query parameter
                if (!response.IsSuccessStatusCode)
                {
                    using var qClient = _httpClientFactory.CreateClient();
                    qClient.Timeout = TimeSpan.FromSeconds(8);
                    try { qClient.DefaultRequestHeaders.Host = "localhost"; } catch { }
                    response = await qClient.GetAsync($"{cleanBase}/api/v3/system/status?apikey={Uri.EscapeDataString(cleanKey)}", cancellationToken);
                }

                // 3. If still 400 (Bad Request - Host filtering), explicitly send with HttpRequestMessage Host header
                if (!response.IsSuccessStatusCode && response.StatusCode == HttpStatusCode.BadRequest)
                {
                    using var hostClient = _httpClientFactory.CreateClient();
                    hostClient.Timeout = TimeSpan.FromSeconds(8);
                    using var req = new HttpRequestMessage(HttpMethod.Get, $"{cleanBase}/api/v3/system/status");
                    req.Headers.Add("X-Api-Key", cleanKey);
                    req.Headers.Host = "localhost";
                    response = await hostClient.SendAsync(req, cancellationToken);
                }
            }

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                var version = root.TryGetProperty("version", out var v) ? v.GetString() : "Unknown";
                var appName = root.TryGetProperty("appName", out var a) ? a.GetString() : "Sonarr";

                _settings.BaseUrl = cleanBase;
                _settings.ApiKey = cleanKey;

                return new SonarrStatusDto
                {
                    IsConfigured = true,
                    IsConnected = true,
                    Version = version,
                    AppName = appName
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                var recoveredUrl = await ProbeHealthyBaseUrlAsync(cleanKey, cancellationToken);
                if (!string.IsNullOrWhiteSpace(recoveredUrl) && !string.Equals(recoveredUrl, cleanBase, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        using var recClient = _httpClientFactory.CreateClient();
                        recClient.Timeout = TimeSpan.FromSeconds(8);
                        recClient.DefaultRequestHeaders.Add("X-Api-Key", cleanKey);
                        var recResp = await recClient.GetAsync($"{recoveredUrl}/api/v3/system/status", cancellationToken);
                        if (recResp.IsSuccessStatusCode)
                        {
                            var recContent = await recResp.Content.ReadAsStringAsync(cancellationToken);
                            using var doc = JsonDocument.Parse(recContent);
                            var root = doc.RootElement;
                            var recVersion = root.TryGetProperty("version", out var v) ? v.GetString() : "Unknown";
                            var recAppName = root.TryGetProperty("appName", out var a) ? a.GetString() : "Sonarr";

                            _settings.BaseUrl = recoveredUrl;
                            _settings.ApiKey = cleanKey;

                            return new SonarrStatusDto
                            {
                                IsConfigured = true,
                                IsConnected = true,
                                Version = recVersion,
                                AppName = recAppName
                            };
                        }
                    }
                    catch { }
                }
            }

            var errBody = string.Empty;
            try { errBody = await response.Content.ReadAsStringAsync(cancellationToken); } catch { }

            var errDetail = !string.IsNullOrWhiteSpace(errBody)
                ? $"Sonarr returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}): {errBody}"
                : $"Sonarr returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase})";

            return new SonarrStatusDto
            {
                IsConfigured = true,
                IsConnected = false,
                ErrorMessage = errDetail
            };
        }
        catch (Exception ex)
        {
            // Probe fallback endpoints
            var recoveredUrl = await ProbeHealthyBaseUrlAsync(cleanKey, cancellationToken);
            if (!string.IsNullOrWhiteSpace(recoveredUrl) && !string.Equals(recoveredUrl, cleanBase, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var fallbackClient = _httpClientFactory.CreateClient();
                    fallbackClient.Timeout = TimeSpan.FromSeconds(8);
                    fallbackClient.DefaultRequestHeaders.Add("X-Api-Key", cleanKey);
                    var fbResponse = await fallbackClient.GetAsync($"{recoveredUrl.TrimEnd('/')}/api/v3/system/status", cancellationToken);
                    if (fbResponse.IsSuccessStatusCode)
                    {
                        var fbContent = await fbResponse.Content.ReadAsStringAsync(cancellationToken);
                        using var fbDoc = JsonDocument.Parse(fbContent);
                        var fbRoot = fbDoc.RootElement;
                        _settings.BaseUrl = recoveredUrl;
                        return new SonarrStatusDto
                        {
                            IsConfigured = true,
                            IsConnected = true,
                            Version = fbRoot.TryGetProperty("version", out var v) ? v.GetString() : "Unknown",
                            AppName = fbRoot.TryGetProperty("appName", out var a) ? a.GetString() : "Sonarr"
                        };
                    }
                }
                catch { }
            }

            return new SonarrStatusDto
            {
                IsConfigured = !string.IsNullOrWhiteSpace(cleanKey),
                IsConnected = false,
                ErrorMessage = $"Failed to connect to Sonarr at {cleanBase}: {ex.Message}"
            };
        }
    }

    public async Task<List<SonarrSeriesDto>> SearchSeriesAsync(string query, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var response = await client.GetAsync($"/api/v3/series/lookup?term={Uri.EscapeDataString(query)}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(content);

        var list = new List<SonarrSeriesDto>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            list.Add(ParseSeriesElement(el));
        }

        await CrossReferenceWithHomelabAsync(list, cancellationToken);
        return list;
    }

    public async Task<List<SonarrSeriesDto>> GetLibrarySeriesAsync(CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/api/v3/series", cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(content);

        var list = new List<SonarrSeriesDto>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            list.Add(ParseSeriesElement(el));
        }

        await CrossReferenceWithHomelabAsync(list, cancellationToken);
        return list;
    }

    public async Task<SonarrSeriesDto?> GetSeriesByIdAsync(int seriesId, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var response = await client.GetAsync($"/api/v3/series/{seriesId}", cancellationToken);
        if (!response.IsSuccessStatusCode) return null;

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(content);
        var dto = ParseSeriesElement(doc.RootElement);

        await CrossReferenceWithHomelabAsync(new List<SonarrSeriesDto> { dto }, cancellationToken);
        return dto;
    }

    public async Task<List<SonarrEpisodeDto>> GetEpisodesAsync(int seriesId, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var response = await client.GetAsync($"/api/v3/episode?seriesId={seriesId}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(content);

        var list = new List<SonarrEpisodeDto>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var ep = new SonarrEpisodeDto
            {
                Id = el.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0,
                SeriesId = el.TryGetProperty("seriesId", out var sIdProp) ? sIdProp.GetInt32() : seriesId,
                EpisodeFileId = el.TryGetProperty("episodeFileId", out var efIdProp) ? efIdProp.GetInt32() : 0,
                SeasonNumber = el.TryGetProperty("seasonNumber", out var snProp) ? snProp.GetInt32() : 0,
                EpisodeNumber = el.TryGetProperty("episodeNumber", out var enProp) ? enProp.GetInt32() : 0,
                Title = el.TryGetProperty("title", out var tProp) ? (tProp.GetString() ?? "") : "",
                AirDate = el.TryGetProperty("airDate", out var adProp) ? adProp.GetString() : null,
                Overview = el.TryGetProperty("overview", out var ovProp) ? ovProp.GetString() : null,
                HasFile = el.TryGetProperty("hasFile", out var hfProp) && hfProp.GetBoolean(),
                Monitored = el.TryGetProperty("monitored", out var mProp) && mProp.GetBoolean()
            };

            list.Add(ep);
        }

        // Cross-reference with MyTube library
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VideoDbContext>();
            var localVideos = await db.Videos
                .Where(v => v.ScanStatus == ScanState.Ready)
                .Select(v => new { v.Id, v.FileName, v.DisplayTitle })
                .ToListAsync(cancellationToken);

            foreach (var ep in list)
            {
                // Try matching S01E02 or episode title in local filename
                var sCode = $"S{ep.SeasonNumber:D2}E{ep.EpisodeNumber:D2}";
                var match = localVideos.FirstOrDefault(v =>
                    v.FileName.Contains(sCode, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(ep.Title) && v.DisplayTitle.Contains(ep.Title, StringComparison.OrdinalIgnoreCase)));

                if (match != null)
                {
                    ep.LocalVideoId = match.Id;
                }
            }
        }

        return list;
    }

    public async Task<SonarrSeriesDto?> AddSeriesAsync(SonarrAddSeriesRequest request, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();

        // 1. Resolve Root Folder
        var folders = await GetRootFoldersAsync(cancellationToken);
        string rootFolder;

        if (folders.Count > 0)
        {
            var requestedPath = request.RootFolderPath ?? _settings.RootFolderPath;
            var matched = !string.IsNullOrWhiteSpace(requestedPath)
                ? folders.FirstOrDefault(f => f.Path.Equals(requestedPath, StringComparison.OrdinalIgnoreCase))
                : null;
            rootFolder = matched?.Path ?? folders[0].Path;
        }
        else
        {
            rootFolder = request.RootFolderPath ?? _settings.RootFolderPath ?? "/tv";
            try
            {
                var rfContent = new StringContent(JsonSerializer.Serialize(new { path = rootFolder }), Encoding.UTF8, "application/json");
                await client.PostAsync("/api/v3/rootfolder", rfContent, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not auto-register root folder {RootFolder} in Sonarr.", rootFolder);
            }
        }

        var payload = new
        {
            title = request.Title,
            tvdbId = request.TvdbId,
            qualityProfileId = request.QualityProfileId > 0 ? request.QualityProfileId : _settings.QualityProfileId,
            rootFolderPath = rootFolder,
            monitored = request.Monitored,
            seasonFolder = request.SeasonFolder,
            seriesType = request.SeriesType,
            addOptions = new
            {
                searchForMissingEpisodes = request.SearchForMissingEpisodes
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/v3/series", content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError($"Sonarr AddSeries failed: {err}");
            throw new InvalidOperationException($"Sonarr failed to add series: {err}");
        }

        var resJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(resJson);
        return ParseSeriesElement(doc.RootElement);
    }

    public async Task<List<SonarrReleaseDto>> GetReleasesAsync(int seriesId, int? episodeId = null, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var url = episodeId.HasValue && episodeId.Value > 0
            ? $"/api/v3/release?episodeId={episodeId.Value}"
            : $"/api/v3/release?seriesId={seriesId}";

        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(content);

        var list = new List<SonarrReleaseDto>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var rel = new SonarrReleaseDto
            {
                Guid = el.TryGetProperty("guid", out var g) ? (g.GetString() ?? "") : "",
                Title = el.TryGetProperty("title", out var t) ? (t.GetString() ?? "") : "",
                Size = el.TryGetProperty("size", out var s) ? s.GetInt64() : 0,
                Indexer = el.TryGetProperty("indexer", out var i) ? (i.GetString() ?? "") : "",
                IndexerId = el.TryGetProperty("indexerId", out var ii) ? ii.GetInt32() : 0,
                Seeders = el.TryGetProperty("seeders", out var sd) ? sd.GetInt32() : null,
                Leechers = el.TryGetProperty("leechers", out var lc) ? lc.GetInt32() : null,
                Protocol = el.TryGetProperty("protocol", out var pr) ? (pr.GetString() ?? "torrent") : "torrent",
                Approved = el.TryGetProperty("approved", out var ap) && ap.GetBoolean()
            };

            if (el.TryGetProperty("quality", out var qObj) && qObj.TryGetProperty("quality", out var qqObj) && qqObj.TryGetProperty("name", out var qName))
            {
                rel.Quality = qName.GetString() ?? "";
            }

            if (el.TryGetProperty("rejections", out var rejArray) && rejArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var r in rejArray.EnumerateArray())
                {
                    rel.Rejections.Add(r.GetString() ?? "");
                }
            }

            list.Add(rel);
        }

        return list;
    }

    public async Task<bool> DownloadReleaseAsync(SonarrDownloadReleaseRequest request, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var payload = new
        {
            guid = request.Guid,
            indexerId = request.IndexerId,
            seriesId = request.SeriesId,
            episodeId = request.EpisodeId
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/v3/release", content, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<List<SonarrQueueItemDto>> GetQueueAsync(CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/api/v3/queue?includeSeries=true&includeEpisode=true", cancellationToken);
        if (!response.IsSuccessStatusCode) return new List<SonarrQueueItemDto>();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(content);

        var list = new List<SonarrQueueItemDto>();
        JsonElement records;

        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            records = doc.RootElement;
        }
        else if (doc.RootElement.TryGetProperty("records", out var r))
        {
            records = r;
        }
        else
        {
            return list;
        }

        foreach (var el in records.EnumerateArray())
        {
            var item = new SonarrQueueItemDto
            {
                Id = el.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0,
                SeriesId = el.TryGetProperty("seriesId", out var sIdProp) ? sIdProp.GetInt32() : 0,
                EpisodeId = el.TryGetProperty("episodeId", out var epIdProp) ? epIdProp.GetInt32() : 0,
                Size = el.TryGetProperty("size", out var sProp) ? (long)sProp.GetDouble() : 0,
                Sizeleft = el.TryGetProperty("sizeleft", out var slProp) ? slProp.GetDouble() : 0,
                Status = el.TryGetProperty("status", out var stProp) ? (stProp.GetString() ?? "") : "",
                TrackedDownloadStatus = el.TryGetProperty("trackedDownloadStatus", out var tds) ? (tds.GetString() ?? "") : "",
                Timeleft = el.TryGetProperty("timeleft", out var tlProp) ? tlProp.GetString() : null
            };

            if (el.TryGetProperty("series", out var sObj) && sObj.TryGetProperty("title", out var st))
            {
                item.SeriesTitle = st.GetString() ?? "";
            }
            if (el.TryGetProperty("episode", out var eObj))
            {
                item.EpisodeTitle = eObj.TryGetProperty("title", out var et) ? (et.GetString() ?? "") : "";
                item.SeasonNumber = eObj.TryGetProperty("seasonNumber", out var sn) ? sn.GetInt32() : 0;
                item.EpisodeNumber = eObj.TryGetProperty("episodeNumber", out var en) ? en.GetInt32() : 0;
            }

            if (item.Size > 0)
            {
                var downloaded = item.Size - item.Sizeleft;
                item.Progress = Math.Clamp(Math.Round((downloaded / (double)item.Size) * 100, 1), 0, 100);
            }

            list.Add(item);
        }

        return list;
    }

    public async Task<List<SonarrQualityProfileDto>> GetQualityProfilesAsync(CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/api/v3/qualityprofile", cancellationToken);
        if (!response.IsSuccessStatusCode) return new List<SonarrQualityProfileDto>();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(content);

        var list = new List<SonarrQualityProfileDto>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            list.Add(new SonarrQualityProfileDto
            {
                Id = el.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0,
                Name = el.TryGetProperty("name", out var nProp) ? (nProp.GetString() ?? "") : ""
            });
        }
        return list;
    }

    public async Task<List<SonarrRootFolderDto>> GetRootFoldersAsync(CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/api/v3/rootfolder", cancellationToken);
        var list = new List<SonarrRootFolderDto>();

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(content);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                list.Add(new SonarrRootFolderDto
                {
                    Id = el.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0,
                    Path = el.TryGetProperty("path", out var pProp) ? (pProp.GetString() ?? "") : "",
                    FreeSpace = el.TryGetProperty("freeSpace", out var fsProp) ? fsProp.GetInt64() : 0
                });
            }
        }

        // If Sonarr has no root folders configured yet, automatically register default /tv
        if (list.Count == 0)
        {
            var defaultPath = !string.IsNullOrWhiteSpace(_settings.RootFolderPath) ? _settings.RootFolderPath : "/tv";
            try
            {
                var rfContent = new StringContent(JsonSerializer.Serialize(new { path = defaultPath }), Encoding.UTF8, "application/json");
                var postResp = await client.PostAsync("/api/v3/rootfolder", rfContent, cancellationToken);
                if (postResp.IsSuccessStatusCode)
                {
                    var postJson = await postResp.Content.ReadAsStringAsync(cancellationToken);
                    using var postDoc = JsonDocument.Parse(postJson);
                    var el = postDoc.RootElement;
                    list.Add(new SonarrRootFolderDto
                    {
                        Id = el.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 1,
                        Path = el.TryGetProperty("path", out var pProp) ? (pProp.GetString() ?? defaultPath) : defaultPath,
                        FreeSpace = el.TryGetProperty("freeSpace", out var fsProp) ? fsProp.GetInt64() : 0
                    });
                    _logger.LogInformation("Auto-registered default root folder {Path} in Sonarr", defaultPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not auto-register default root folder {Path} in Sonarr", defaultPath);
            }
        }

        return list;
    }

    private SonarrSeriesDto ParseSeriesElement(JsonElement el)
    {
        var dto = new SonarrSeriesDto
        {
            Id = el.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0,
            Title = el.TryGetProperty("title", out var tProp) ? (tProp.GetString() ?? "") : "",
            CleanTitle = el.TryGetProperty("cleanTitle", out var ctProp) ? ctProp.GetString() : null,
            SortTitle = el.TryGetProperty("sortTitle", out var stProp) ? stProp.GetString() : null,
            Status = el.TryGetProperty("status", out var sProp) ? (sProp.GetString() ?? "") : "",
            Overview = el.TryGetProperty("overview", out var ovProp) ? ovProp.GetString() : null,
            Network = el.TryGetProperty("network", out var netProp) ? netProp.GetString() : null,
            Year = el.TryGetProperty("year", out var yrProp) ? yrProp.GetInt32() : 0,
            Path = el.TryGetProperty("path", out var pProp) ? pProp.GetString() : null,
            QualityProfileId = el.TryGetProperty("qualityProfileId", out var qpProp) ? qpProp.GetInt32() : 1,
            SeasonFolder = el.TryGetProperty("seasonFolder", out var sfProp) && sfProp.GetBoolean(),
            Monitored = el.TryGetProperty("monitored", out var mProp) && mProp.GetBoolean(),
            TvdbId = el.TryGetProperty("tvdbId", out var tvdbProp) ? tvdbProp.GetInt32() : 0,
            ImdbId = el.TryGetProperty("imdbId", out var imdbProp) ? imdbProp.GetString() : null,
            SeriesType = el.TryGetProperty("seriesType", out var stypeProp) ? stypeProp.GetString() : "standard"
        };

        if (el.TryGetProperty("statistics", out var statsObj))
        {
            dto.SeasonCount = statsObj.TryGetProperty("seasonCount", out var sc) ? sc.GetInt32() : 0;
            dto.TotalEpisodeCount = statsObj.TryGetProperty("totalEpisodeCount", out var tec) ? tec.GetInt32() : 0;
            dto.EpisodeCount = statsObj.TryGetProperty("episodeCount", out var ec) ? ec.GetInt32() : 0;
            dto.EpisodeFileCount = statsObj.TryGetProperty("episodeFileCount", out var efc) ? efc.GetInt32() : 0;
            dto.SizeOnDisk = statsObj.TryGetProperty("sizeOnDisk", out var sod) ? sod.GetInt64() : 0;
        }

        if (el.TryGetProperty("images", out var imgArr) && imgArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var img in imgArr.EnumerateArray())
            {
                var coverType = img.TryGetProperty("coverType", out var ct) ? ct.GetString() : "";
                var url = img.TryGetProperty("remoteUrl", out var ru) ? ru.GetString() : (img.TryGetProperty("url", out var u) ? u.GetString() : null);

                if (coverType?.Equals("poster", StringComparison.OrdinalIgnoreCase) == true && dto.PosterUrl == null)
                {
                    dto.PosterUrl = url;
                }
                else if (coverType?.Equals("fanart", StringComparison.OrdinalIgnoreCase) == true && dto.FanartUrl == null)
                {
                    dto.FanartUrl = url;
                }
            }
        }

        if (el.TryGetProperty("genres", out var gArr) && gArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var g in gArr.EnumerateArray())
            {
                dto.Genres.Add(g.GetString() ?? "");
            }
        }

        if (el.TryGetProperty("ratings", out var ratObj) && ratObj.TryGetProperty("value", out var rv))
        {
            dto.RatingsValue = rv.GetDouble();
        }

        if (el.TryGetProperty("seasons", out var seaArr) && seaArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in seaArr.EnumerateArray())
            {
                var season = new SonarrSeasonDto
                {
                    SeasonNumber = s.TryGetProperty("seasonNumber", out var sn) ? sn.GetInt32() : 0,
                    Monitored = s.TryGetProperty("monitored", out var sm) && sm.GetBoolean()
                };
                if (s.TryGetProperty("statistics", out var sStats))
                {
                    season.TotalEpisodeCount = sStats.TryGetProperty("totalEpisodeCount", out var tec) ? tec.GetInt32() : 0;
                    season.EpisodeCount = sStats.TryGetProperty("episodeCount", out var ec) ? ec.GetInt32() : 0;
                    season.EpisodeFileCount = sStats.TryGetProperty("episodeFileCount", out var efc) ? efc.GetInt32() : 0;
                    season.SizeOnDisk = sStats.TryGetProperty("sizeOnDisk", out var sod) ? sod.GetInt64() : 0;
                }
                dto.Seasons.Add(season);
            }
        }

        return dto;
    }

    private async Task CrossReferenceWithHomelabAsync(List<SonarrSeriesDto> seriesList, CancellationToken cancellationToken)
    {
        if (seriesList.Count == 0) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VideoDbContext>();
            var localTitles = await db.Videos
                .Where(v => v.ScanStatus == ScanState.Ready)
                .Select(v => new { v.DisplayTitle, v.FileName })
                .ToListAsync(cancellationToken);

            foreach (var s in seriesList)
            {
                var cleanTitle = s.Title.Replace(":", "").Replace("-", " ").Trim();
                var matched = localTitles.Count(v =>
                    v.DisplayTitle.Contains(cleanTitle, StringComparison.OrdinalIgnoreCase) ||
                    v.FileName.Contains(cleanTitle.Replace(" ", "."), StringComparison.OrdinalIgnoreCase));

                s.IsAvailableInHomelab = matched > 0;
                s.HomelabMatchedEpisodes = matched;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cross-reference Sonarr series with local library.");
        }
    }
}
