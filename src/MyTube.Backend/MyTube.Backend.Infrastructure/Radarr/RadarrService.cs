using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using MyTube.Backend.Application.Prowlarr;
using MyTube.Backend.Application.Radarr;
using MyTube.Backend.Domain;
using MyTube.Backend.Infrastructure.Media;
using MyTube.Backend.Infrastructure.Servarr;

namespace MyTube.Backend.Infrastructure.Radarr;

public class RadarrService : IRadarrService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RadarrService> _logger;
    private readonly IProwlarrService? _prowlarrService;
    private RadarrSettings _settings;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public RadarrService(
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<RadarrService> logger,
        IProwlarrService? prowlarrService = null)
    {
        _httpClientFactory = httpClientFactory;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
        _prowlarrService = prowlarrService;

        _settings = new RadarrSettings
        {
            BaseUrl = configuration["Radarr:BaseUrl"] ?? "http://radarr:7878",
            ApiKey = configuration["Radarr:ApiKey"] ?? string.Empty,
            RootFolderPath = configuration["Radarr:RootFolderPath"] ?? "/movies",
            QualityProfileId = int.TryParse(configuration["Radarr:QualityProfileId"], out var qp) ? qp : 1
        };

        EnsureApiKeyLoaded();
    }

    private void EnsureApiKeyLoaded()
    {
        if (!string.IsNullOrWhiteSpace(_settings.ApiKey)) return;

        var candidates = new[]
        {
            "/radarr-config/config.xml",
            "/config/config.xml",
            Path.Combine(AppContext.BaseDirectory, "radarr-config", "config.xml")
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
                        _settings.ApiKey = match.Groups[1].Value.Trim();
                        _logger.LogInformation("Auto-detected Radarr API key from {Path}", path);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not read Radarr config from {Path}", path);
            }
        }
    }

    private async Task<string?> ProbeHealthyBaseUrlAsync(string apiKey, CancellationToken cancellationToken)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(_settings.BaseUrl))
            candidates.Add(_settings.BaseUrl);

        candidates.Add("http://radarr:7878");
        candidates.Add("http://localhost:7878");
        candidates.Add("http://127.0.0.1:7878");
        candidates.Add("http://host.docker.internal:7878");
        candidates.Add("http://192.168.1.16:7878");

        var distinct = candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var candidate in distinct)
        {
            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri(candidate.TrimEnd('/'));
                client.Timeout = TimeSpan.FromSeconds(2);
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
                }

                var resp = await client.GetAsync("/api/v3/system/status", cancellationToken);
                if (resp.IsSuccessStatusCode || resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    if (!string.Equals(_settings.BaseUrl, candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Automatically recovered Radarr connection by switching BaseUrl from {Old} to {New}", _settings.BaseUrl, candidate);
                        _settings.BaseUrl = candidate;
                    }
                    return candidate;
                }
            }
            catch
            {
                // Continue probing
            }
        }

        return null;
    }

    private string MediaRoot => _configuration["Media:RootPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "media");
    private string NonDeletableRadarrFolder => Path.Combine(MediaRoot, "Radarr");

    public RadarrSettings GetCurrentSettings()
    {
        EnsureApiKeyLoaded();
        return _settings;
    }

    public async Task UpdateSettingsAsync(RadarrSettings newSettings, CancellationToken cancellationToken = default)
    {
        _settings.BaseUrl = (newSettings.BaseUrl ?? "http://radarr:7878").TrimEnd('/');
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
                using var doc = JsonDocument.Parse(json);
                var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json, _jsonOptions) ?? new();
                dict["Radarr"] = new
                {
                    BaseUrl = _settings.BaseUrl,
                    ApiKey = _settings.ApiKey,
                    RootFolderPath = _settings.RootFolderPath,
                    QualityProfileId = _settings.QualityProfileId
                };
                var updatedJson = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(appSettingsPath, updatedJson, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not persist updated Radarr settings to appsettings.json, keeping in memory.");
        }
    }

    public async Task<RadarrStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        EnsureApiKeyLoaded();
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

    public async Task<RadarrStatusDto> TestConnectionAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default)
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

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(effectiveApiKey))
        {
            return new RadarrStatusDto
            {
                IsConfigured = false,
                IsConnected = false,
                ErrorMessage = "Radarr Base URL and API Key must be configured."
            };
        }

        try
        {
            var cleanBase = baseUrl.TrimEnd('/');
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("X-Api-Key", effectiveApiKey.Trim());

            var response = await client.GetAsync($"{cleanBase}/api/v3/system/status", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new RadarrStatusDto
                {
                    IsConfigured = true,
                    IsConnected = false,
                    ErrorMessage = $"Radarr returned HTTP {response.StatusCode}: {response.ReasonPhrase}"
                };
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            return new RadarrStatusDto
            {
                IsConfigured = true,
                IsConnected = true,
                Version = root.TryGetProperty("version", out var v) ? v.GetString() : null,
                AppName = root.TryGetProperty("appName", out var a) ? a.GetString() : "Radarr"
            };
        }
        catch (Exception ex)
        {
            var recoveredUrl = await ProbeHealthyBaseUrlAsync(effectiveApiKey, cancellationToken);
            if (!string.IsNullOrWhiteSpace(recoveredUrl) && !string.Equals(recoveredUrl, baseUrl, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var fbClient = _httpClientFactory.CreateClient();
                    fbClient.Timeout = TimeSpan.FromSeconds(6);
                    fbClient.DefaultRequestHeaders.Add("X-Api-Key", effectiveApiKey.Trim());
                    var fbResp = await fbClient.GetAsync($"{recoveredUrl.TrimEnd('/')}/api/v3/system/status", cancellationToken);
                    if (fbResp.IsSuccessStatusCode)
                    {
                        var fbContent = await fbResp.Content.ReadAsStringAsync(cancellationToken);
                        using var fbDoc = JsonDocument.Parse(fbContent);
                        var fbRoot = fbDoc.RootElement;
                        return new RadarrStatusDto
                        {
                            IsConfigured = true,
                            IsConnected = true,
                            Version = fbRoot.TryGetProperty("version", out var v) ? v.GetString() : null,
                            AppName = fbRoot.TryGetProperty("appName", out var a) ? a.GetString() : "Radarr"
                        };
                    }
                }
                catch { }
            }

            _logger.LogWarning(ex, "Failed to connect to Radarr at {BaseUrl}", baseUrl);
            return new RadarrStatusDto
            {
                IsConfigured = true,
                IsConnected = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<List<RadarrMovieDto>> SearchMoviesAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<RadarrMovieDto>();

        var client = CreateClient();
        var url = $"{_settings.BaseUrl.TrimEnd('/')}/api/v3/movie/lookup?term={Uri.EscapeDataString(query.Trim())}";
        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var rawMovies = JsonSerializer.Deserialize<List<JsonElement>>(content, _jsonOptions) ?? new();

        var movies = rawMovies.Select(MapJsonToMovieDto).ToList();
        await EnrichWithHomelabAvailabilityAsync(movies, cancellationToken);
        return movies;
    }

    public async Task<List<RadarrMovieDto>> GetLibraryMoviesAsync(CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        var url = $"{_settings.BaseUrl.TrimEnd('/')}/api/v3/movie";
        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var rawMovies = JsonSerializer.Deserialize<List<JsonElement>>(content, _jsonOptions) ?? new();

        var movies = rawMovies.Select(MapJsonToMovieDto).ToList();
        await EnrichWithHomelabAvailabilityAsync(movies, cancellationToken);
        foreach (var m in movies)
        {
            PopulateExtraFilesForMovie(m);
        }
        return movies;
    }

    public async Task<RadarrMovieDto?> AddMovieAsync(RadarrAddMovieRequest request, CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        var cleanBase = _settings.BaseUrl.TrimEnd('/');

        // 1. Ensure Root Folder is registered in Radarr
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
            rootFolder = request.RootFolderPath ?? _settings.RootFolderPath ?? "/movies";
            try
            {
                var rfContent = new StringContent(JsonSerializer.Serialize(new { path = rootFolder }), Encoding.UTF8, "application/json");
                await client.PostAsync($"{cleanBase}/api/v3/rootfolder", rfContent, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not auto-register root folder {RootFolder} in Radarr.", rootFolder);
            }
        }

        // 2. Resolve Quality Profile ID
        var profileId = request.QualityProfileId ?? _settings.QualityProfileId;
        if (profileId <= 0)
        {
            var profiles = await GetQualityProfilesAsync(cancellationToken);
            profileId = profiles.FirstOrDefault()?.Id ?? 1;
        }

        // 3. Lookup full movie details from Radarr by tmdb ID
        var lookupUrl = $"{cleanBase}/api/v3/movie/lookup?term=tmdb:{request.TmdbId}";
        var lookupResp = await client.GetAsync(lookupUrl, cancellationToken);
        JsonElement moviePayload;

        if (lookupResp.IsSuccessStatusCode)
        {
            var lookupJson = await lookupResp.Content.ReadAsStringAsync(cancellationToken);
            var list = JsonSerializer.Deserialize<List<JsonElement>>(lookupJson, _jsonOptions);
            moviePayload = list != null && list.Count > 0 ? list[0] : default;
        }
        else
        {
            // Fallback: title search lookup
            var searchMovies = await client.GetAsync($"{cleanBase}/api/v3/movie/lookup?term={Uri.EscapeDataString(request.Title)}", cancellationToken);
            searchMovies.EnsureSuccessStatusCode();
            var searchJson = await searchMovies.Content.ReadAsStringAsync(cancellationToken);
            var list = JsonSerializer.Deserialize<List<JsonElement>>(searchJson, _jsonOptions) ?? new();
            moviePayload = list.FirstOrDefault(m => m.TryGetProperty("tmdbId", out var t) && t.GetInt32() == request.TmdbId);
            if (moviePayload.ValueKind == JsonValueKind.Undefined && list.Count > 0)
                moviePayload = list[0];
        }

        if (moviePayload.ValueKind == JsonValueKind.Undefined)
        {
            throw new InvalidOperationException($"Could not locate movie details for TMDb ID {request.TmdbId}.");
        }

        // Build add payload
        var addDict = JsonSerializer.Deserialize<Dictionary<string, object>>(moviePayload.GetRawText(), _jsonOptions) ?? new();
        addDict.Remove("id");
        addDict.Remove("path");
        addDict["rootFolderPath"] = rootFolder;
        addDict["qualityProfileId"] = profileId;
        addDict["monitored"] = true;
        addDict["addOptions"] = new Dictionary<string, object>
        {
            { "searchForMovie", request.SearchForMovie }
        };

        var postContent = new StringContent(JsonSerializer.Serialize(addDict), Encoding.UTF8, "application/json");
        var postResponse = await client.PostAsync($"{cleanBase}/api/v3/movie", postContent, cancellationToken);

        if (!postResponse.IsSuccessStatusCode)
        {
            var errorBody = await postResponse.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Radarr returned {StatusCode} when adding movie: {ErrorBody}", postResponse.StatusCode, errorBody);

            // If movie has already been added, find it and trigger search command if requested
            if (errorBody.Contains("already been added", StringComparison.OrdinalIgnoreCase) ||
                errorBody.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                var library = await GetLibraryMoviesAsync(cancellationToken);
                var existing = library.FirstOrDefault(m => m.TmdbId == request.TmdbId);
                if (existing != null)
                {
                    if (request.SearchForMovie)
                    {
                        var cmdPayload = new { name = "MoviesSearch", movieIds = new[] { existing.Id } };
                        var cmdContent = new StringContent(JsonSerializer.Serialize(cmdPayload), Encoding.UTF8, "application/json");
                        await client.PostAsync($"{cleanBase}/api/v3/command", cmdContent, cancellationToken);
                    }
                    return existing;
                }
            }

            throw new InvalidOperationException($"Radarr error ({postResponse.StatusCode}): {errorBody}");
        }

        var resultJson = await postResponse.Content.ReadAsStringAsync(cancellationToken);
        var createdElement = JsonSerializer.Deserialize<JsonElement>(resultJson, _jsonOptions);
        var createdDto = MapJsonToMovieDto(createdElement);

        await EnrichWithHomelabAvailabilityAsync(new List<RadarrMovieDto> { createdDto }, cancellationToken);
        return createdDto;
    }

    public async Task<RadarrAutoGrabResultDto> AutoGrabMovieAsync(RadarrAddMovieRequest request, CancellationToken cancellationToken = default)
    {
        var cleanBase = _settings.BaseUrl.TrimEnd('/');
        RadarrMovieDto? movie = null;

        try
        {
            var addReq = new RadarrAddMovieRequest
            {
                TmdbId = request.TmdbId,
                Title = request.Title,
                Year = request.Year,
                QualityProfileId = request.QualityProfileId,
                RootFolderPath = request.RootFolderPath,
                PosterUrl = request.PosterUrl,
                SearchForMovie = false
            };
            movie = await AddMovieAsync(addReq, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not add movie {Title} before auto-grab; checking existing library...", request.Title);
        }

        if (movie == null || movie.Id <= 0)
        {
            var lib = await GetLibraryMoviesAsync(cancellationToken);
            movie = lib.FirstOrDefault(m => m.TmdbId == request.TmdbId)
                 ?? lib.FirstOrDefault(m => m.Title.Equals(request.Title, StringComparison.OrdinalIgnoreCase));
        }

        var radarrMovieId = movie?.Id ?? 0;

        // 1. Try Radarr release search and direct grab
        if (radarrMovieId > 0)
        {
            try
            {
                var releases = await GetReleasesAsync(radarrMovieId, cancellationToken);
                if (releases != null && releases.Count > 0)
                {
                    // Approved releases allowed for download
                    var approved = releases.Where(r => r.Approved && r.DownloadAllowed).ToList();
                    var healthy = approved.Where(r => (r.Seeders ?? 0) > 0).ToList();
                    var candidates = healthy.Count > 0 ? healthy : approved;

                    if (candidates.Count > 0)
                    {
                        var best = candidates.OrderByDescending(r => r.Seeders ?? 0)
                                             .ThenByDescending(r => r.Size)
                                             .First();

                        var dlSuccess = await DownloadReleaseAsync(new RadarrDownloadReleaseRequest
                        {
                            Guid = best.Guid,
                            IndexerId = best.IndexerId
                        }, cancellationToken);

                        if (dlSuccess)
                        {
                            return new RadarrAutoGrabResultDto
                            {
                                Success = true,
                                Grabbed = true,
                                ReleaseTitle = best.Title,
                                Quality = best.Quality,
                                Indexer = best.Indexer,
                                Seeders = best.Seeders,
                                Size = best.Size,
                                Source = "Radarr",
                                Message = $"Grabbed release '{best.Title}' ({best.Quality}, {best.Seeders ?? 0} seeders) via {best.Indexer} into Transmission!",
                                Movie = movie
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Radarr release lookup failed for movieId {MovieId}", radarrMovieId);
            }
        }

        // 2. Direct Prowlarr fallback search (searches raw trackers if Radarr parsing failed)
        if (_prowlarrService != null)
        {
            try
            {
                var prowlarrReleases = await _prowlarrService.SearchAsync(request.Title, "movies", cancellationToken);
                if (prowlarrReleases != null && prowlarrReleases.Count > 0)
                {
                    // Extract significant words from movie title (ignoring generic words)
                    var titleWords = request.Title
                        .Split(new[] { ' ', '.', '_', '-', ':', '(', ')', '\'', '"' }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(w => w.Length > 2 && !new[] { "the", "and", "movie", "movies", "film", "films" }.Contains(w.ToLowerInvariant()))
                        .ToList();

                    var matchingProwlarr = prowlarrReleases.Where(r =>
                    {
                        if (r.Seeders == null || r.Seeders <= 0) return false;
                        var rTitle = r.Title.ToLowerInvariant();
                        if (titleWords.Count > 0 && !titleWords.Any(w => rTitle.Contains(w.ToLowerInvariant())))
                            return false;
                        return true;
                    }).ToList();

                    if (matchingProwlarr.Count > 0)
                    {
                        var yearStr = request.Year > 1900 ? request.Year.ToString() : null;
                        var yearMinusOne = request.Year > 1900 ? (request.Year - 1).ToString() : null;
                        var yearPlusOne = request.Year > 1900 ? (request.Year + 1).ToString() : null;

                        var yearMatches = matchingProwlarr.Where(r =>
                            yearStr != null && (r.Title.Contains(yearStr) || (yearMinusOne != null && r.Title.Contains(yearMinusOne)) || (yearPlusOne != null && r.Title.Contains(yearPlusOne)))
                        ).ToList();

                        var bestProwlarr = (yearMatches.Count > 0 ? yearMatches : matchingProwlarr)
                            .OrderByDescending(r => r.Seeders ?? 0)
                            .ThenByDescending(r => r.Size)
                            .First();

                        var dlSuccess = await _prowlarrService.DownloadAsync(new ProwlarrDownloadRequest
                        {
                            Guid = bestProwlarr.Guid,
                            Title = bestProwlarr.Title,
                            DownloadUrl = bestProwlarr.DownloadUrl,
                            MagnetUrl = bestProwlarr.MagnetUrl
                        }, cancellationToken);

                        if (dlSuccess)
                        {
                            return new RadarrAutoGrabResultDto
                            {
                                Success = true,
                                Grabbed = true,
                                ReleaseTitle = bestProwlarr.Title,
                                Quality = bestProwlarr.CategoryBadge,
                                Indexer = bestProwlarr.Indexer,
                                Seeders = bestProwlarr.Seeders,
                                Size = bestProwlarr.Size,
                                Source = "Prowlarr (Direct)",
                                Message = $"Auto-grabbed release '{bestProwlarr.Title}' from {bestProwlarr.Indexer} ({bestProwlarr.Seeders} seeders) via VPN directly to Transmission!",
                                Movie = movie
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Prowlarr fallback search failed for {Title}", request.Title);
            }
        }

        // 3. If no release was found to download:
        // Trigger Radarr's background search command so it continues monitoring RSS feeds for this movie
        if (radarrMovieId > 0)
        {
            try
            {
                var client = CreateClient();
                var cmdPayload = new { name = "MoviesSearch", movieIds = new[] { radarrMovieId } };
                var cmdContent = new StringContent(JsonSerializer.Serialize(cmdPayload), Encoding.UTF8, "application/json");
                await client.PostAsync($"{cleanBase}/api/v3/command", cmdContent, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not post MoviesSearch command to Radarr");
            }
        }

        return new RadarrAutoGrabResultDto
        {
            Success = false,
            Grabbed = false,
            Message = $"No active torrent releases found for \"{request.Title}\" ({request.Year}) on indexers. Radarr has added it to your library and will auto-grab it when released.",
            Movie = movie
        };
    }

    public async Task<List<RadarrReleaseDto>> GetReleasesAsync(int movieId, CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        var url = $"{_settings.BaseUrl.TrimEnd('/')}/api/v3/release?movieId={movieId}";
        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var rawReleases = JsonSerializer.Deserialize<List<JsonElement>>(content, _jsonOptions) ?? new();

        var list = new List<RadarrReleaseDto>();
        foreach (var r in rawReleases)
        {
            var dto = new RadarrReleaseDto
            {
                Guid = r.TryGetProperty("guid", out var g) ? g.GetString() ?? string.Empty : string.Empty,
                Title = r.TryGetProperty("title", out var t) ? t.GetString() ?? string.Empty : string.Empty,
                Size = r.TryGetProperty("size", out var s) && s.TryGetInt64(out var sizeVal) ? sizeVal : 0,
                Indexer = r.TryGetProperty("indexer", out var idx) ? idx.GetString() ?? string.Empty : string.Empty,
                IndexerId = r.TryGetProperty("indexerId", out var idxId) && idxId.TryGetInt32(out var idVal) ? idVal : 0,
                Seeders = r.TryGetProperty("seeders", out var seed) && seed.TryGetInt32(out var seedVal) ? seedVal : null,
                Leechers = r.TryGetProperty("leechers", out var leech) && leech.TryGetInt32(out var leechVal) ? leechVal : null,
                Protocol = r.TryGetProperty("protocol", out var proto) ? proto.GetString() ?? "torrent" : "torrent",
                AgeHours = r.TryGetProperty("ageHours", out var age) && age.TryGetDouble(out var ageVal) ? ageVal : 0,
                Approved = r.TryGetProperty("approved", out var app) && app.GetBoolean(),
                DownloadAllowed = r.TryGetProperty("downloadAllowed", out var da) && da.GetBoolean()
            };

            if (r.TryGetProperty("quality", out var q) && q.TryGetProperty("quality", out var qObj) && qObj.TryGetProperty("name", out var qName))
            {
                dto.Quality = qName.GetString() ?? string.Empty;
            }

            if (r.TryGetProperty("publishDate", out var pub) && pub.TryGetDateTime(out var dt))
            {
                dto.PublishDate = dt;
            }

            if (r.TryGetProperty("rejections", out var rej) && rej.ValueKind == JsonValueKind.Array)
            {
                dto.Rejections = rej.EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(x => !string.IsNullOrEmpty(x)).ToList();
            }

            list.Add(dto);
        }

        // Sort by approved first, then seeders descending, then size descending
        return list.OrderByDescending(x => x.Approved)
                   .ThenByDescending(x => x.Seeders ?? 0)
                   .ThenByDescending(x => x.Size)
                   .ToList();
    }

    public async Task<bool> DownloadReleaseAsync(RadarrDownloadReleaseRequest request, CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        var payload = new
        {
            guid = request.Guid,
            indexerId = request.IndexerId
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{_settings.BaseUrl.TrimEnd('/')}/api/v3/release", content, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<List<RadarrQueueItemDto>> GetQueueAsync(CancellationToken cancellationToken = default)
    {
        var queue = new List<RadarrQueueItemDto>();

        try
        {
            var client = CreateClient();
            var url = $"{_settings.BaseUrl.TrimEnd('/')}/api/v3/queue";
            var response = await client.GetAsync(url, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(content);

                var itemsList = new List<JsonElement>();
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    itemsList.AddRange(doc.RootElement.EnumerateArray());
                }
                else if (doc.RootElement.TryGetProperty("records", out var records) && records.ValueKind == JsonValueKind.Array)
                {
                    itemsList.AddRange(records.EnumerateArray());
                }

                foreach (var item in itemsList)
                {
                    var q = new RadarrQueueItemDto
                    {
                        Id = item.TryGetProperty("id", out var id) && id.TryGetInt32(out var idVal) ? idVal : 0,
                        MovieId = item.TryGetProperty("movieId", out var mId) && mId.TryGetInt32(out var mVal) ? mVal : 0,
                        ReleaseTitle = item.TryGetProperty("title", out var t) ? t.GetString() : null,
                        Status = item.TryGetProperty("status", out var st) ? st.GetString() ?? "queued" : "queued",
                        Size = item.TryGetProperty("size", out var s) && s.TryGetDouble(out var sVal) ? sVal : 0,
                        SizeLeft = item.TryGetProperty("sizeleft", out var sl) && sl.TryGetDouble(out var slVal) ? slVal : 0,
                        TimeLeft = item.TryGetProperty("timeleft", out var tl) ? tl.GetString() : null,
                        Protocol = item.TryGetProperty("protocol", out var pr) ? pr.GetString() : null,
                        DownloadClient = item.TryGetProperty("downloadClient", out var dc) ? dc.GetString() : null,
                        ErrorMessage = item.TryGetProperty("errorMessage", out var err) ? err.GetString() : null
                    };

                    if (item.TryGetProperty("movie", out var movie) && movie.TryGetProperty("title", out var mt))
                    {
                        q.MovieTitle = mt.GetString();
                    }

                    if (item.TryGetProperty("estimatedCompletionTime", out var est) && est.TryGetDateTime(out var estDt))
                    {
                        q.EstimatedCompletionTime = estDt;
                    }

                    if (q.Size > 0)
                    {
                        var downloaded = q.Size - q.SizeLeft;
                        q.ProgressPercentage = Math.Round(Math.Clamp((downloaded / q.Size) * 100.0, 0.0, 100.0), 1);
                    }

                    queue.Add(q);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query Radarr queue.");
        }

        // Live real-time progress, download speed, and peers directly from Transmission
        if (_prowlarrService != null)
        {
            try
            {
                var txTorrents = await _prowlarrService.GetTransmissionTorrentsAsync(cancellationToken);
                var matchedTxIds = new HashSet<int>();

                foreach (var q in queue)
                {
                    var matched = txTorrents.FirstOrDefault(tx =>
                        (!string.IsNullOrEmpty(q.ReleaseTitle) && (tx.Name.Contains(q.ReleaseTitle, StringComparison.OrdinalIgnoreCase) || q.ReleaseTitle.Contains(tx.Name, StringComparison.OrdinalIgnoreCase))) ||
                        (!string.IsNullOrEmpty(q.MovieTitle) && tx.Name.Contains(q.MovieTitle, StringComparison.OrdinalIgnoreCase)));

                    if (matched != null)
                    {
                        matchedTxIds.Add(matched.Id);
                        q.TransmissionId = matched.Id;
                        q.DownloadSpeed = matched.RateDownload;
                        q.UploadSpeed = matched.RateUpload;
                        q.ConnectedPeers = matched.PeersSendingToUs;
                        q.ProgressPercentage = Math.Round(Math.Clamp(matched.PercentDone * 100.0, 0.0, 100.0), 1);
                        if (matched.SizeWhenDone > 0)
                        {
                            q.Size = matched.SizeWhenDone;
                            q.SizeLeft = matched.LeftUntilDone;
                        }

                        q.MetadataPercentComplete = matched.MetadataPercentComplete;

                        if (matched.PercentDone >= 1.0 || matched.Status == 6)
                        {
                            q.Status = "completed";
                            q.TimeLeft = "Completed";
                        }
                        else if (matched.Status == 0)
                        {
                            q.Status = "paused";
                            q.TimeLeft = "Paused";
                        }
                        else if (matched.MetadataPercentComplete < 1.0 || matched.SizeWhenDone == 0)
                        {
                            q.Status = "fetching metadata";
                            q.TimeLeft = "Connecting to seeders";
                        }
                        else if (matched.RateDownload > 0)
                        {
                            q.Status = "downloading";
                            if (matched.LeftUntilDone > 0)
                            {
                                var secondsLeft = matched.LeftUntilDone / matched.RateDownload;
                                var ts = TimeSpan.FromSeconds(secondsLeft);
                                q.TimeLeft = ts.TotalHours >= 1 ? $"{Math.Floor(ts.TotalHours):00}:{ts.Minutes:00}:{ts.Seconds:00}" : $"{ts.Minutes:00}:{ts.Seconds:00}";
                            }
                        }

                        q.FilesCount = matched.FilesCount;
                        q.IsDirectory = matched.IsDirectory;
                        var movieParam = q.MovieId > 0 ? $"&movieId={q.MovieId}" : "";
                        q.FilePath = $"/api/radarr/torrents/{matched.Id}/download" + (matched.FilesCount > 1 || matched.IsDirectory ? "?zip=true" : "?zip=false") + movieParam;
                        if (matched.Files.Count > 0)
                        {
                            q.Files = matched.Files.Select((f, idx) => new RadarrFileItemDto
                            {
                                FileName = Path.GetFileName(f.Name),
                                FilePath = f.Name,
                                FileSizeBytes = f.Length,
                                FormattedSize = FormatSize(f.Length),
                                FileType = ClassifyFileType(f.Name),
                                DownloadUrl = $"/api/radarr/torrents/{matched.Id}/download?fileIndex={idx}{movieParam}"
                            }).ToList();
                        }
                    }
                }

                // Also include any active Transmission torrents not in Radarr queue yet
                foreach (var tx in txTorrents.Where(t => !matchedTxIds.Contains(t.Id)))
                {
                    var displayTitle = tx.Name;
                    if (IsHexHash(displayTitle))
                    {
                        var cached = _prowlarrService.ResolveCachedTitle(tx.HashString) ?? _prowlarrService.ResolveCachedTitle(tx.Name);
                        if (!string.IsNullOrWhiteSpace(cached))
                        {
                            displayTitle = cached;
                        }
                    }

                    var isFetchingMetadata = tx.MetadataPercentComplete < 1.0 || tx.SizeWhenDone == 0;
                    var status = tx.PercentDone >= 1.0 || tx.Status == 6 ? "completed" :
                                 tx.Status == 0 ? "paused" :
                                 isFetchingMetadata ? "fetching metadata" :
                                 tx.Status == 4 ? "downloading" :
                                 tx.Status is 1 or 2 ? "verifying" : "queued";

                    var q = new RadarrQueueItemDto
                    {
                        Id = 0,
                        TransmissionId = tx.Id,
                        MovieTitle = displayTitle,
                        ReleaseTitle = displayTitle,
                        Size = tx.SizeWhenDone,
                        SizeLeft = tx.LeftUntilDone,
                        ProgressPercentage = Math.Round(Math.Clamp(tx.PercentDone * 100.0, 0.0, 100.0), 1),
                        DownloadSpeed = tx.RateDownload,
                        UploadSpeed = tx.RateUpload,
                        ConnectedPeers = tx.PeersSendingToUs,
                        Protocol = "torrent",
                        DownloadClient = "Transmission (VPN)",
                        FilesCount = tx.FilesCount,
                        IsDirectory = tx.IsDirectory,
                        MetadataPercentComplete = tx.MetadataPercentComplete,
                        FilePath = $"/api/radarr/torrents/{tx.Id}/download" + (tx.FilesCount > 1 || tx.IsDirectory ? "?zip=true" : ""),
                        Status = status
                    };

                    if (tx.Files.Count > 0)
                    {
                        q.Files = tx.Files.Select((f, idx) => new RadarrFileItemDto
                        {
                            FileName = Path.GetFileName(f.Name),
                            FilePath = f.Name,
                            FileSizeBytes = f.Length,
                            FormattedSize = FormatSize(f.Length),
                            FileType = ClassifyFileType(f.Name),
                            DownloadUrl = $"/api/radarr/torrents/{tx.Id}/download?fileIndex={idx}"
                        }).ToList();
                    }

                    if (tx.RateDownload > 0 && tx.LeftUntilDone > 0)
                    {
                        var secondsLeft = tx.LeftUntilDone / tx.RateDownload;
                        var ts = TimeSpan.FromSeconds(secondsLeft);
                        q.TimeLeft = ts.TotalHours >= 1 ? $"{Math.Floor(ts.TotalHours):00}:{ts.Minutes:00}:{ts.Seconds:00}" : $"{ts.Minutes:00}:{ts.Seconds:00}";
                    }
                    else if (tx.PercentDone >= 1.0 || tx.Status == 6)
                    {
                        q.TimeLeft = "Completed";
                    }

                    queue.Add(q);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query Transmission for live download progress.");
            }
        }

        return queue;
    }

    public async Task<bool> CancelQueueItemAsync(int id, int? transmissionId = null, bool deleteData = true, CancellationToken cancellationToken = default)
    {
        var success = false;
        var txId = transmissionId;

        // If we don't have txId, try to resolve from queue or fallback to id
        if ((!txId.HasValue || txId.Value <= 0) && id > 0)
        {
            try
            {
                var queue = await GetQueueAsync(cancellationToken);
                var matched = queue.FirstOrDefault(q => q.Id == id || (q.TransmissionId.HasValue && q.TransmissionId.Value == id));
                if (matched?.TransmissionId.HasValue == true && matched.TransmissionId.Value > 0)
                {
                    txId = matched.TransmissionId.Value;
                }
                else
                {
                    txId = id;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve transmission ID from queue for item {Id}", id);
                txId = id;
            }
        }

        // 1. If we have a Radarr Queue ID, remove from Radarr
        if (id > 0)
        {
            try
            {
                var client = CreateClient();
                var url = $"{_settings.BaseUrl.TrimEnd('/')}/api/v3/queue/{id}?removeFromClient=true&blocklist=false";
                var response = await client.DeleteAsync(url, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Deleted queue item {Id} from Radarr", id);
                    success = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete item {Id} from Radarr queue", id);
            }
        }

        // 2. If we have a Transmission ID, remove directly from Transmission
        if (_prowlarrService != null && txId.HasValue && txId.Value > 0)
        {
            try
            {
                var removed = await _prowlarrService.RemoveTransmissionTorrentAsync(txId.Value, deleteData, cancellationToken);
                if (removed)
                {
                    _logger.LogInformation("Deleted torrent {Id} from Transmission", txId.Value);
                    success = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete torrent {Id} from Transmission", txId.Value);
            }
        }

        return success;
    }

    public async Task<bool> PauseQueueItemAsync(int id, int? transmissionId = null, CancellationToken cancellationToken = default)
    {
        if (_prowlarrService == null)
            return false;

        var txId = transmissionId;
        if (!txId.HasValue || txId.Value <= 0)
        {
            var queue = await GetQueueAsync(cancellationToken);
            var matched = queue.FirstOrDefault(q => q.Id == id || (q.TransmissionId.HasValue && q.TransmissionId.Value == id));
            if (matched != null && matched.TransmissionId.HasValue && matched.TransmissionId.Value > 0)
            {
                txId = matched.TransmissionId.Value;
            }
            else if (id > 0)
            {
                txId = id;
            }
        }

        if (txId.HasValue && txId.Value > 0)
        {
            return await _prowlarrService.PauseTransmissionTorrentAsync(txId.Value, cancellationToken);
        }

        _logger.LogWarning("Could not find Transmission ID to pause queue item {Id}", id);
        return false;
    }

    public async Task<bool> ResumeQueueItemAsync(int id, int? transmissionId = null, CancellationToken cancellationToken = default)
    {
        if (_prowlarrService == null)
            return false;

        var txId = transmissionId;
        if (!txId.HasValue || txId.Value <= 0)
        {
            var queue = await GetQueueAsync(cancellationToken);
            var matched = queue.FirstOrDefault(q => q.Id == id || (q.TransmissionId.HasValue && q.TransmissionId.Value == id));
            if (matched != null && matched.TransmissionId.HasValue && matched.TransmissionId.Value > 0)
            {
                txId = matched.TransmissionId.Value;
            }
            else if (id > 0)
            {
                txId = id;
            }
        }

        if (txId.HasValue && txId.Value > 0)
        {
            return await _prowlarrService.ResumeTransmissionTorrentAsync(txId.Value, cancellationToken);
        }

        _logger.LogWarning("Could not find Transmission ID to resume queue item {Id}", id);
        return false;
    }

    public async Task<bool> KickstartQueueItemAsync(int id, int? transmissionId = null, CancellationToken cancellationToken = default)
    {
        if (_prowlarrService == null)
            return false;

        var txId = transmissionId;
        if (!txId.HasValue || txId.Value <= 0)
        {
            var queue = await GetQueueAsync(cancellationToken);
            var matched = queue.FirstOrDefault(q => q.Id == id || (q.TransmissionId.HasValue && q.TransmissionId.Value == id));
            if (matched != null && matched.TransmissionId.HasValue && matched.TransmissionId.Value > 0)
            {
                txId = matched.TransmissionId.Value;
            }
            else if (id > 0)
            {
                txId = id;
            }
        }

        if (txId.HasValue && txId.Value > 0)
        {
            _logger.LogInformation("Kickstarting Transmission torrent {TxId} for queue item {Id}", txId.Value, id);
            return await _prowlarrService.KickstartTransmissionTorrentAsync(txId.Value, cancellationToken);
        }

        _logger.LogWarning("Could not find Transmission ID to kickstart queue item {Id}", id);
        return false;
    }

    private static bool IsHexHash(string? str)
    {
        if (string.IsNullOrWhiteSpace(str) || (str.Length != 40 && str.Length != 32))
            return false;
        return str.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
    }

    public async Task<List<RadarrQualityProfileDto>> GetQualityProfilesAsync(CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        var url = $"{_settings.BaseUrl.TrimEnd('/')}/api/v3/qualityprofile";
        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var rawProfiles = JsonSerializer.Deserialize<List<JsonElement>>(content, _jsonOptions) ?? new();

        return rawProfiles.Select(p => new RadarrQualityProfileDto
        {
            Id = p.TryGetProperty("id", out var id) ? id.GetInt32() : 0,
            Name = p.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty
        }).ToList();
    }

    public async Task<List<RadarrRootFolderDto>> GetRootFoldersAsync(CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        var url = $"{_settings.BaseUrl.TrimEnd('/')}/api/v3/rootfolder";
        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var rawFolders = JsonSerializer.Deserialize<List<JsonElement>>(content, _jsonOptions) ?? new();

        return rawFolders.Select(f => new RadarrRootFolderDto
        {
            Id = f.TryGetProperty("id", out var id) ? id.GetInt32() : 0,
            Path = f.TryGetProperty("path", out var p) ? p.GetString() ?? string.Empty : string.Empty,
            FreeSpace = f.TryGetProperty("freeSpace", out var fs) && fs.TryGetInt64(out var fsVal) ? fsVal : 0
        }).ToList();
    }

    private HttpClient CreateClient()
    {
        EnsureApiKeyLoaded();
        var client = _httpClientFactory.CreateClient();
        ServarrClientHelper.ConfigureClient(client, _settings.BaseUrl, _settings.ApiKey, TimeSpan.FromSeconds(25));
        return client;
    }

    private RadarrMovieDto MapJsonToMovieDto(JsonElement m)
    {
        var dto = new RadarrMovieDto
        {
            Id = m.TryGetProperty("id", out var id) && id.TryGetInt32(out var idVal) ? idVal : 0,
            Title = m.TryGetProperty("title", out var t) ? t.GetString() ?? string.Empty : string.Empty,
            OriginalTitle = m.TryGetProperty("originalTitle", out var ot) ? ot.GetString() : null,
            Year = m.TryGetProperty("year", out var y) && y.TryGetInt32(out var yVal) ? yVal : 0,
            TmdbId = m.TryGetProperty("tmdbId", out var tmdb) && tmdb.TryGetInt32(out var tmdbVal) ? tmdbVal : 0,
            ImdbId = m.TryGetProperty("imdbId", out var imdb) ? imdb.GetString() : null,
            Overview = m.TryGetProperty("overview", out var ov) ? ov.GetString() : null,
            Runtime = m.TryGetProperty("runtime", out var rt) && rt.TryGetInt32(out var rtVal) ? rtVal : 0,
            HasFile = m.TryGetProperty("hasFile", out var hf) && hf.GetBoolean(),
            Monitored = m.TryGetProperty("monitored", out var mon) && mon.GetBoolean(),
            SizeOnDisk = m.TryGetProperty("sizeOnDisk", out var sod) && sod.TryGetInt64(out var sodVal) ? sodVal : null
        };

        if (m.TryGetProperty("path", out var pObj))
        {
            dto.MovieFolderPath = pObj.GetString();
        }

        if (m.TryGetProperty("movieFile", out var mf) && mf.ValueKind == JsonValueKind.Object)
        {
            if (mf.TryGetProperty("id", out var mfid) && mfid.TryGetInt32(out var fileIdVal))
                dto.MovieFileId = fileIdVal;

            if (mf.TryGetProperty("relativePath", out var rp))
                dto.MovieFilePath = rp.GetString();
            else if (mf.TryGetProperty("path", out var p))
                dto.MovieFilePath = p.GetString();

            if (mf.TryGetProperty("quality", out var qObj) && qObj.TryGetProperty("quality", out var qInner) && qInner.TryGetProperty("name", out var qName))
                dto.Quality = qName.GetString();

            if (mf.TryGetProperty("mediaInfo", out var miObj))
            {
                if (miObj.TryGetProperty("videoCodec", out var vc)) dto.VideoCodec = vc.GetString();
                if (miObj.TryGetProperty("audioFormat", out var af)) dto.AudioFormat = af.GetString();
            }
        }

        if (dto.Id > 0)
        {
            dto.DownloadUrl = $"/api/radarr/movies/{dto.Id}/download";
            dto.PosterDownloadUrl = $"/api/radarr/movies/{dto.Id}/poster/download";
            dto.FanartDownloadUrl = $"/api/radarr/movies/{dto.Id}/fanart/download";
        }

        if (m.TryGetProperty("ratings", out var rat) && rat.TryGetProperty("tmdb", out var tmdbRat) && tmdbRat.TryGetProperty("value", out var val))
        {
            dto.RatingsValue = val.GetDouble();
        }

        if (m.TryGetProperty("genres", out var g) && g.ValueKind == JsonValueKind.Array)
        {
            dto.Genres = g.EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(x => !string.IsNullOrEmpty(x)).ToList();
        }

        // Parse images (Poster & Fanart)
        if (m.TryGetProperty("images", out var imgs) && imgs.ValueKind == JsonValueKind.Array)
        {
            foreach (var img in imgs.EnumerateArray())
            {
                var coverType = img.TryGetProperty("coverType", out var ct) ? ct.GetString() : null;
                var remoteUrl = img.TryGetProperty("remoteUrl", out var ru) ? ru.GetString() : null;
                var url = img.TryGetProperty("url", out var u) ? u.GetString() : null;

                var finalUrl = !string.IsNullOrEmpty(remoteUrl)
                    ? remoteUrl
                    : (!string.IsNullOrEmpty(url) ? $"{_settings.BaseUrl.TrimEnd('/')}{url}?apikey={_settings.ApiKey}" : null);

                if (string.Equals(coverType, "poster", StringComparison.OrdinalIgnoreCase))
                {
                    dto.PosterUrl = finalUrl;
                }
                else if (string.Equals(coverType, "fanart", StringComparison.OrdinalIgnoreCase))
                {
                    dto.FanartUrl = finalUrl;
                }
            }
        }

        return dto;
    }

    public async Task<RadarrDownloadSummaryDto> GetDownloadedMediaSummaryAsync(CancellationToken cancellationToken = default)
    {
        var library = await GetLibraryMoviesAsync(cancellationToken);
        var downloaded = library.Where(m => m.HasFile).ToList();
        long totalBytes = downloaded.Sum(m => m.SizeOnDisk ?? 0);

        return new RadarrDownloadSummaryDto
        {
            TotalDownloadedCount = downloaded.Count,
            TotalSizeBytes = totalBytes,
            FormattedTotalSize = FormatSize(totalBytes),
            NonDeletableFolderPath = NonDeletableRadarrFolder,
            ProtectedFolderName = "Radarr",
            DownloadedMovies = downloaded
        };
    }

    public async Task<bool> SyncRadarrMediaLinksAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(NonDeletableRadarrFolder))
            {
                Directory.CreateDirectory(NonDeletableRadarrFolder);
                _logger.LogInformation("Created non-deletable Radarr folder at {Path}", NonDeletableRadarrFolder);
            }

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<VideoDbContext>();
            var radarrCategory = await dbContext.Categories.FirstOrDefaultAsync(c => c.Name == "Radarr", cancellationToken);
            if (radarrCategory == null)
            {
                radarrCategory = new Category { Name = "Radarr" };
                dbContext.Categories.Add(radarrCategory);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var movies = await GetLibraryMoviesAsync(cancellationToken);
            var downloadedMovies = movies.Where(m => m.HasFile).ToList();

            foreach (var movie in downloadedMovies)
            {
                var folderName = $"{SanitizeFileName(movie.Title)} ({movie.Year})";
                var movieTargetFolder = Path.Combine(NonDeletableRadarrFolder, folderName);
                if (!Directory.Exists(movieTargetFolder))
                {
                    Directory.CreateDirectory(movieTargetFolder);
                }

                var sourceVideoFile = ResolvePhysicalVideoFile(movie);
                if (!string.IsNullOrEmpty(sourceVideoFile) && File.Exists(sourceVideoFile))
                {
                    var targetVideoFile = Path.Combine(movieTargetFolder, Path.GetFileName(sourceVideoFile));
                    if (!sourceVideoFile.Equals(targetVideoFile, StringComparison.OrdinalIgnoreCase))
                    {
                        TryCreateFileLink(sourceVideoFile, targetVideoFile);
                    }

                    var effectivePath = File.Exists(targetVideoFile) ? targetVideoFile : sourceVideoFile;
                    var existingVideo = await dbContext.Videos.FirstOrDefaultAsync(v => v.FilePath == targetVideoFile || v.FilePath == sourceVideoFile, cancellationToken);
                    if (existingVideo != null)
                    {
                        existingVideo.CategoryId = radarrCategory.Id;
                    }
                    else
                    {
                        var fi = new FileInfo(effectivePath);
                        dbContext.Videos.Add(new Video
                        {
                            FilePath = effectivePath,
                            FileName = Path.GetFileName(fi.FullName),
                            DisplayTitle = movie.Title,
                            FileSize = fi.Length,
                            LastModified = fi.LastWriteTimeUtc,
                            CategoryId = radarrCategory.Id,
                            ScanStatus = ScanState.Ready,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                // Copy or link any extra files (PDFs, subtitles, nfos) from source folder to target folder
                var sourceFolder = ResolveLocalMovieFolderPath(movie);
                if (!string.IsNullOrEmpty(sourceFolder) && Directory.Exists(sourceFolder) && !sourceFolder.Equals(movieTargetFolder, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        foreach (var extraFile in Directory.GetFiles(sourceFolder, "*.*", SearchOption.AllDirectories))
                        {
                            var targetExtra = Path.Combine(movieTargetFolder, Path.GetFileName(extraFile));
                            if (!File.Exists(targetExtra))
                            {
                                TryCreateFileLink(extraFile, targetExtra);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error linking extra files for movie {Title}", movie.Title);
                    }
                }

                // Download poster and fanart locally if present
                if (!string.IsNullOrEmpty(movie.PosterUrl))
                {
                    var posterPath = Path.Combine(movieTargetFolder, "poster.jpg");
                    if (!File.Exists(posterPath))
                    {
                        _ = Task.Run(async () => await DownloadRemoteImageToFileAsync(movie.PosterUrl, posterPath));
                    }
                }

                if (!string.IsNullOrEmpty(movie.FanartUrl))
                {
                    var fanartPath = Path.Combine(movieTargetFolder, "fanart.jpg");
                    if (!File.Exists(fanartPath))
                    {
                        _ = Task.Run(async () => await DownloadRemoteImageToFileAsync(movie.FanartUrl, fanartPath));
                    }
                }

                // Create links.json metadata
                var infoPath = Path.Combine(movieTargetFolder, "movie_links.json");
                var infoObj = new
                {
                    movie.Id,
                    movie.Title,
                    movie.Year,
                    movie.TmdbId,
                    movie.Overview,
                    movie.Quality,
                    VideoDownloadUrl = $"/api/radarr/movies/{movie.Id}/download",
                    PosterDownloadUrl = $"/api/radarr/movies/{movie.Id}/poster/download",
                    FanartDownloadUrl = $"/api/radarr/movies/{movie.Id}/fanart/download",
                    SyncedAtUtc = DateTime.UtcNow
                };
                try
                {
                    await File.WriteAllTextAsync(infoPath, JsonSerializer.Serialize(infoObj, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
                }
                catch { }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync Radarr media links into non-deletable folder.");
            return false;
        }
    }

    public async Task<bool> DeleteMovieFilesAsync(int movieId, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = CreateClient();
            var cleanBase = _settings.BaseUrl.TrimEnd('/');

            var movieResp = await client.GetAsync($"{cleanBase}/api/v3/movie/{movieId}", cancellationToken);
            if (!movieResp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Could not fetch movie {Id} from Radarr for file deletion.", movieId);
                return false;
            }

            var json = await movieResp.Content.ReadAsStringAsync(cancellationToken);
            var movieElement = JsonSerializer.Deserialize<JsonElement>(json, _jsonOptions);
            var movieDto = MapJsonToMovieDto(movieElement);

            // 1. Delete movieFile in Radarr
            if (movieDto.MovieFileId.HasValue && movieDto.MovieFileId.Value > 0)
            {
                try
                {
                    await client.DeleteAsync($"{cleanBase}/api/v3/moviefile/{movieDto.MovieFileId.Value}", cancellationToken);
                    _logger.LogInformation("Deleted Radarr movieFile {FileId}", movieDto.MovieFileId.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete movieFile {FileId} via Radarr API", movieDto.MovieFileId.Value);
                }
            }

            // 2. Clean physical movie folder in NonDeletableRadarrFolder
            var folderName = $"{SanitizeFileName(movieDto.Title)} ({movieDto.Year})";
            var nonDeletableMovieFolder = Path.Combine(NonDeletableRadarrFolder, folderName);
            if (Directory.Exists(nonDeletableMovieFolder))
            {
                try
                {
                    foreach (var f in Directory.GetFiles(nonDeletableMovieFolder, "*", SearchOption.AllDirectories))
                    {
                        File.SetAttributes(f, FileAttributes.Normal);
                        File.Delete(f);
                    }
                    Directory.Delete(nonDeletableMovieFolder, true);
                    _logger.LogInformation("Deleted movie directory inside non-deletable folder: {Path}", nonDeletableMovieFolder);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not delete movie folder {Path}", nonDeletableMovieFolder);
                }
            }

            // 3. Clean source folder if different
            var sourceFolder = ResolveLocalMovieFolderPath(movieDto);
            if (!string.IsNullOrEmpty(sourceFolder) && Directory.Exists(sourceFolder) && !sourceFolder.Equals(NonDeletableRadarrFolder, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    foreach (var f in Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories))
                    {
                        File.SetAttributes(f, FileAttributes.Normal);
                        File.Delete(f);
                    }
                    Directory.Delete(sourceFolder, true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not delete source movie folder {Path}", sourceFolder);
                }
            }

            // 4. Remove database records
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<VideoDbContext>();
            var normalizedTitle = NormalizeTitle(movieDto.Title);
            var dbVideos = await dbContext.Videos.Where(v => v.DisplayTitle.ToLower().Contains(normalizedTitle.ToLower())).ToListAsync(cancellationToken);
            if (dbVideos.Any())
            {
                dbContext.Videos.RemoveRange(dbVideos);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting movie files for movie ID {Id}", movieId);
            return false;
        }
    }

    public async Task<int> DeleteAllDownloadedFilesAsync(CancellationToken cancellationToken = default)
    {
        var movies = await GetLibraryMoviesAsync(cancellationToken);
        var downloaded = movies.Where(m => m.HasFile).ToList();
        int count = 0;

        foreach (var m in downloaded)
        {
            var ok = await DeleteMovieFilesAsync(m.Id, cancellationToken);
            if (ok) count++;
        }

        // Clean any leftover files inside NonDeletableRadarrFolder, but PRESERVE the root NonDeletableRadarrFolder itself!
        if (Directory.Exists(NonDeletableRadarrFolder))
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(NonDeletableRadarrFolder))
                {
                    try { Directory.Delete(dir, true); } catch { }
                }
                foreach (var file in Directory.GetFiles(NonDeletableRadarrFolder))
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                    }
                    catch { }
                }
                _logger.LogInformation("Preserved non-deletable Radarr root folder at {Path}", NonDeletableRadarrFolder);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cleaning leftover files in non-deletable Radarr folder.");
            }
        }

        return count;
    }

    public async Task<int> BatchDeleteMovieFilesAsync(List<int> movieIds, List<string>? filePaths = null, CancellationToken cancellationToken = default)
    {
        int deletedCount = 0;
        if (movieIds != null)
        {
            foreach (var id in movieIds)
            {
                if (await DeleteMovieFilesAsync(id, cancellationToken))
                    deletedCount++;
            }
        }

        if (filePaths != null)
        {
            foreach (var path in filePaths)
            {
                var resolved = ResolvePhysicalFilePath(path);
                if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
                {
                    try
                    {
                        File.SetAttributes(resolved, FileAttributes.Normal);
                        File.Delete(resolved);
                        deletedCount++;
                    }
                    catch { }
                }
            }
        }

        return deletedCount;
    }

    public async Task<(string FilePath, string ContentType, string FileName)?> GetMovieDownloadFileAsync(int movieId, CancellationToken cancellationToken = default)
    {
        var movies = await GetLibraryMoviesAsync(cancellationToken);
        var movie = movies.FirstOrDefault(m => m.Id == movieId);
        if (movie == null) return null;

        var resolvedPath = ResolvePhysicalVideoFile(movie);
        if (string.IsNullOrEmpty(resolvedPath) || !File.Exists(resolvedPath))
        {
            return null;
        }

        var ext = Path.GetExtension(resolvedPath).ToLowerInvariant();
        var contentType = GetMimeType(resolvedPath);
        var safeTitle = SanitizeFileName(movie.Title);
        var fileName = $"{safeTitle} ({movie.Year}){ext}";

        return (resolvedPath, contentType, fileName);
    }

    public async Task<(string FilePath, string ContentType, string FileName)?> GetFileDownloadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        if (string.IsNullOrWhiteSpace(filePath)) return null;

        var resolved = ResolvePhysicalFilePath(filePath);
        if (string.IsNullOrEmpty(resolved) || !File.Exists(resolved))
        {
            return null;
        }

        var contentType = GetMimeType(resolved);
        var fileName = Path.GetFileName(resolved);
        return (resolved, contentType, fileName);
    }

    public async Task<(string ResolvedPath, bool IsDirectory, string ContentType, string FileName)?> GetDownloadTargetAsync(string path, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        if (string.IsNullOrWhiteSpace(path)) return null;

        var resolved = ResolvePhysicalPath(path);
        if (Directory.Exists(resolved))
        {
            var dirName = Path.GetFileName(resolved.TrimEnd('/', '\\'));
            if (string.IsNullOrWhiteSpace(dirName)) dirName = "download";
            return (resolved, true, "application/zip", $"{dirName}.zip");
        }

        if (File.Exists(resolved))
        {
            var contentType = GetMimeType(resolved);
            var fileName = Path.GetFileName(resolved);
            return (resolved, false, contentType, fileName);
        }

        return null;
    }

    public async Task<(string? TargetPath, bool IsDirectory, List<string> Files, string DownloadName, string? ErrorMessage)?> GetTransmissionDownloadInfoAsync(
        int transmissionId,
        int? fileIndex = null,
        int? movieId = null,
        CancellationToken cancellationToken = default)
    {
        TransmissionTorrentDto? torrent = null;
        if (_prowlarrService != null)
        {
            try
            {
                var torrents = await _prowlarrService.GetTransmissionTorrentsAsync(cancellationToken);
                torrent = torrents.FirstOrDefault(t => t.Id == transmissionId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not query Transmission for torrent ID {Id}", transmissionId);
            }
        }

        // Helper to probe candidate file path (exact, .part, or resolved)
        string? FindExistingFilePath(string basePath)
        {
            if (string.IsNullOrWhiteSpace(basePath)) return null;
            if (File.Exists(basePath)) return basePath;
            if (File.Exists(basePath + ".part")) return basePath + ".part";
            var resolved = ResolvePhysicalPath(basePath);
            if (File.Exists(resolved)) return resolved;
            if (File.Exists(resolved + ".part")) return resolved + ".part";
            return null;
        }

        // Helper to probe candidate directory
        string? FindExistingDirectoryPath(string basePath)
        {
            if (string.IsNullOrWhiteSpace(basePath)) return null;
            if (Directory.Exists(basePath)) return basePath;
            var resolved = ResolvePhysicalPath(basePath);
            if (Directory.Exists(resolved)) return resolved;
            return null;
        }

        // 1. If torrent found in Transmission
        if (torrent != null)
        {
            var mappedDownloadDir = !string.IsNullOrWhiteSpace(torrent.DownloadDir)
                ? MapRemotePathToLocal(torrent.DownloadDir)
                : MediaRoot;

            var baseDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                mappedDownloadDir,
                MediaRoot,
                NonDeletableRadarrFolder,
                Path.Combine(MediaRoot, "Downloads"),
                Path.Combine(MediaRoot, "complete")
            };

            // If specific file index requested
            if (fileIndex.HasValue && fileIndex.Value >= 0 && fileIndex.Value < torrent.Files.Count)
            {
                var file = torrent.Files[fileIndex.Value];
                foreach (var dir in baseDirs)
                {
                    var cand1 = FindExistingFilePath(Path.Combine(dir, file.Name));
                    if (cand1 != null)
                        return (cand1, false, new List<string> { cand1 }, Path.GetFileName(cand1), null);

                    var cand2 = FindExistingFilePath(Path.Combine(dir, Path.GetFileName(file.Name)));
                    if (cand2 != null)
                        return (cand2, false, new List<string> { cand2 }, Path.GetFileName(cand2), null);
                }

                var candFallback = FindExistingFilePath(file.Name);
                if (candFallback != null)
                    return (candFallback, false, new List<string> { candFallback }, Path.GetFileName(candFallback), null);
            }

            // Check if top-level directory exists
            foreach (var dir in baseDirs)
            {
                var candDir = FindExistingDirectoryPath(Path.Combine(dir, torrent.Name));
                if (candDir != null)
                {
                    var allFiles = Directory.GetFiles(candDir, "*", SearchOption.AllDirectories).ToList();
                    if (allFiles.Count > 0)
                    {
                        return (candDir, true, allFiles, $"{torrent.Name}.zip", null);
                    }
                }
            }

            // Check if single file matching torrent.Name on disk
            foreach (var dir in baseDirs)
            {
                var candSingle = FindExistingFilePath(Path.Combine(dir, torrent.Name));
                if (candSingle != null)
                    return (candSingle, false, new List<string> { candSingle }, Path.GetFileName(candSingle), null);

                // Try common video extensions if torrent.Name has no extension
                var videoExtensions = new[] { ".mkv", ".mp4", ".avi", ".webm", ".mov" };
                foreach (var ext in videoExtensions)
                {
                    var candExt = FindExistingFilePath(Path.Combine(dir, torrent.Name + ext));
                    if (candExt != null)
                        return (candExt, false, new List<string> { candExt }, Path.GetFileName(candExt), null);
                }
            }

            // Check individual files listed in torrent.Files on disk
            if (torrent.Files.Count > 0)
            {
                var resolvedFiles = new List<string>();
                foreach (var f in torrent.Files)
                {
                    string? match = null;
                    foreach (var dir in baseDirs)
                    {
                        match = FindExistingFilePath(Path.Combine(dir, f.Name)) ??
                                FindExistingFilePath(Path.Combine(dir, Path.GetFileName(f.Name)));
                        if (match != null) break;
                    }
                    match ??= FindExistingFilePath(f.Name);
                    if (match != null && !resolvedFiles.Contains(match, StringComparer.OrdinalIgnoreCase))
                    {
                        resolvedFiles.Add(match);
                    }
                }

                if (resolvedFiles.Count > 1)
                {
                    return (null, true, resolvedFiles, $"{torrent.Name}.zip", null);
                }
                if (resolvedFiles.Count == 1)
                {
                    return (resolvedFiles[0], false, resolvedFiles, Path.GetFileName(resolvedFiles[0]), null);
                }
            }
        }

        // 2. Fallback to Radarr Library Movie (if imported by Radarr or movieId supplied)
        try
        {
            var movies = await GetLibraryMoviesAsync(cancellationToken);
            RadarrMovieDto? matchedMovie = null;

            if (movieId.HasValue && movieId.Value > 0)
            {
                matchedMovie = movies.FirstOrDefault(m => m.Id == movieId.Value);
            }

            if (matchedMovie == null && torrent != null && !string.IsNullOrWhiteSpace(torrent.Name))
            {
                matchedMovie = movies.FirstOrDefault(m =>
                    torrent.Name.Contains(m.Title, StringComparison.OrdinalIgnoreCase) ||
                    m.Title.Contains(torrent.Name, StringComparison.OrdinalIgnoreCase));
            }

            // Also search Radarr queue to see if transmissionId or movieId maps to a movie
            if (matchedMovie == null)
            {
                var queue = await GetQueueAsync(cancellationToken);
                var qItem = queue.FirstOrDefault(q => q.TransmissionId == transmissionId || q.Id == transmissionId || (movieId.HasValue && q.MovieId == movieId.Value));
                if (qItem != null && qItem.MovieId > 0)
                {
                    matchedMovie = movies.FirstOrDefault(m => m.Id == qItem.MovieId);
                }
                if (matchedMovie == null && qItem != null && !string.IsNullOrWhiteSpace(qItem.MovieTitle))
                {
                    matchedMovie = movies.FirstOrDefault(m => string.Equals(m.Title, qItem.MovieTitle, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (matchedMovie != null)
            {
                var videoFile = ResolvePhysicalVideoFile(matchedMovie);
                if (!string.IsNullOrEmpty(videoFile) && File.Exists(videoFile))
                {
                    return (videoFile, false, new List<string> { videoFile }, Path.GetFileName(videoFile), null);
                }

                var folder = ResolveLocalMovieFolderPath(matchedMovie);
                if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                {
                    var allMovieFiles = Directory.GetFiles(folder, "*", SearchOption.AllDirectories).ToList();
                    if (allMovieFiles.Count > 1)
                    {
                        return (folder, true, allMovieFiles, $"{matchedMovie.Title}.zip", null);
                    }
                    if (allMovieFiles.Count == 1)
                    {
                        return (allMovieFiles[0], false, allMovieFiles, Path.GetFileName(allMovieFiles[0]), null);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve Radarr movie fallback for transmission ID {Id}", transmissionId);
        }

        // 3. Informative error message if torrent is currently downloading/paused
        if (torrent != null)
        {
            var pct = Math.Round(Math.Clamp(torrent.PercentDone * 100.0, 0, 100), 1);
            var statusStr = torrent.Status == 4 ? "downloading" :
                            torrent.Status == 0 ? "paused" :
                            torrent.Status is 1 or 2 ? "verifying" : "queued";

            return (null, false, new List<string>(), "", $"Torrent '{torrent.Name}' is {statusStr} ({pct}% complete). Files are not yet written or completed on disk.");
        }

        return (null, false, new List<string>(), "", "Torrent download not found or files not yet available on disk.");
    }


    public async Task<(byte[] ImageBytes, string ContentType, string FileName)?> GetMoviePosterDownloadAsync(int movieId, CancellationToken cancellationToken = default)
    {
        var movies = await GetLibraryMoviesAsync(cancellationToken);
        var movie = movies.FirstOrDefault(m => m.Id == movieId);
        if (movie == null || string.IsNullOrWhiteSpace(movie.PosterUrl)) return null;

        var folder = ResolveLocalMovieFolderPath(movie);
        if (!string.IsNullOrEmpty(folder))
        {
            var localPoster = Path.Combine(folder, "poster.jpg");
            if (File.Exists(localPoster))
            {
                var bytes = await File.ReadAllBytesAsync(localPoster, cancellationToken);
                return (bytes, "image/jpeg", $"{SanitizeFileName(movie.Title)}_poster.jpg");
            }
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var resp = await client.GetAsync(movie.PosterUrl, cancellationToken);
            if (resp.IsSuccessStatusCode)
            {
                var bytes = await resp.Content.ReadAsByteArrayAsync(cancellationToken);
                var ctype = resp.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                return (bytes, ctype, $"{SanitizeFileName(movie.Title)}_poster.jpg");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download poster image for movie {Title}", movie.Title);
        }

        return null;
    }

    public async Task<(byte[] ImageBytes, string ContentType, string FileName)?> GetMovieFanartDownloadAsync(int movieId, CancellationToken cancellationToken = default)
    {
        var movies = await GetLibraryMoviesAsync(cancellationToken);
        var movie = movies.FirstOrDefault(m => m.Id == movieId);
        if (movie == null || string.IsNullOrWhiteSpace(movie.FanartUrl)) return null;

        var folder = ResolveLocalMovieFolderPath(movie);
        if (!string.IsNullOrEmpty(folder))
        {
            var localFanart = Path.Combine(folder, "fanart.jpg");
            if (File.Exists(localFanart))
            {
                var bytes = await File.ReadAllBytesAsync(localFanart, cancellationToken);
                return (bytes, "image/jpeg", $"{SanitizeFileName(movie.Title)}_fanart.jpg");
            }
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var resp = await client.GetAsync(movie.FanartUrl, cancellationToken);
            if (resp.IsSuccessStatusCode)
            {
                var bytes = await resp.Content.ReadAsByteArrayAsync(cancellationToken);
                var ctype = resp.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                return (bytes, ctype, $"{SanitizeFileName(movie.Title)}_fanart.jpg");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download fanart image for movie {Title}", movie.Title);
        }

        return null;
    }

    private void PopulateExtraFilesForMovie(RadarrMovieDto dto)
    {
        if (dto == null) return;
        var filesList = new List<RadarrFileItemDto>();

        if (dto.HasFile && !string.IsNullOrWhiteSpace(dto.MovieFilePath))
        {
            var fileName = Path.GetFileName(dto.MovieFilePath);
            filesList.Add(new RadarrFileItemDto
            {
                FileName = fileName,
                FilePath = dto.MovieFilePath,
                RelativePath = fileName,
                FileSizeBytes = dto.SizeOnDisk ?? 0,
                FormattedSize = FormatSize(dto.SizeOnDisk ?? 0),
                FileType = "video",
                DownloadUrl = $"/api/radarr/movies/{dto.Id}/download"
            });
        }

        var physicalFolder = ResolveLocalMovieFolderPath(dto);
        if (!string.IsNullOrEmpty(physicalFolder) && Directory.Exists(physicalFolder))
        {
            try
            {
                var allFilesOnDisk = Directory.GetFiles(physicalFolder, "*.*", SearchOption.AllDirectories);
                foreach (var file in allFilesOnDisk)
                {
                    var name = Path.GetFileName(file);
                    if (filesList.Any(f => f.FileName.Equals(name, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    var fi = new FileInfo(file);
                    var fileType = ClassifyFileType(file);
                    var relPath = Path.GetRelativePath(physicalFolder, file);

                    filesList.Add(new RadarrFileItemDto
                    {
                        FileName = name,
                        FilePath = file,
                        RelativePath = relPath,
                        FileSizeBytes = fi.Length,
                        FormattedSize = FormatSize(fi.Length),
                        FileType = fileType,
                        DownloadUrl = $"/api/radarr/files/download?path={Uri.EscapeDataString(file)}"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error scanning physical extra files for movie {Title}", dto.Title);
            }
        }

        if (!string.IsNullOrEmpty(dto.PosterUrl) && !filesList.Any(f => f.FileName.Equals("poster.jpg", StringComparison.OrdinalIgnoreCase)))
        {
            filesList.Add(new RadarrFileItemDto
            {
                FileName = $"{SanitizeFileName(dto.Title)}_poster.jpg",
                FilePath = dto.PosterUrl,
                RelativePath = "poster.jpg",
                FileSizeBytes = 0,
                FormattedSize = "Image",
                FileType = "image",
                DownloadUrl = $"/api/radarr/movies/{dto.Id}/poster/download"
            });
        }

        if (!string.IsNullOrEmpty(dto.FanartUrl) && !filesList.Any(f => f.FileName.Equals("fanart.jpg", StringComparison.OrdinalIgnoreCase)))
        {
            filesList.Add(new RadarrFileItemDto
            {
                FileName = $"{SanitizeFileName(dto.Title)}_fanart.jpg",
                FilePath = dto.FanartUrl,
                RelativePath = "fanart.jpg",
                FileSizeBytes = 0,
                FormattedSize = "Backdrop",
                FileType = "image",
                DownloadUrl = $"/api/radarr/movies/{dto.Id}/fanart/download"
            });
        }

        dto.ExtraFiles = filesList;
    }

    private string? ResolvePhysicalVideoFile(RadarrMovieDto movie)
    {
        if (movie == null) return null;

        if (!string.IsNullOrWhiteSpace(movie.LocalVideoPath) && File.Exists(movie.LocalVideoPath))
            return movie.LocalVideoPath;

        if (!string.IsNullOrWhiteSpace(movie.MovieFilePath))
        {
            var mapped = MapRemotePathToLocal(movie.MovieFilePath);
            if (File.Exists(mapped)) return mapped;

            var cand = ResolvePhysicalFilePath(movie.MovieFilePath);
            if (File.Exists(cand)) return cand;
        }

        var folder = ResolveLocalMovieFolderPath(movie);
        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
        {
            var videoExtensions = new[] { ".mp4", ".mkv", ".avi", ".mov", ".webm" };
            var videoFile = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories)
                .FirstOrDefault(f => videoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
            if (videoFile != null) return videoFile;
        }

        return null;
    }

    private string? ResolveLocalMovieFolderPath(RadarrMovieDto dto)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(dto.MovieFolderPath))
        {
            var mapped = MapRemotePathToLocal(dto.MovieFolderPath);
            candidates.Add(mapped);
        }

        var safeTitle = $"{SanitizeFileName(dto.Title)} ({dto.Year})";
        candidates.Add(Path.Combine(NonDeletableRadarrFolder, safeTitle));
        candidates.Add(Path.Combine(NonDeletableRadarrFolder, SanitizeFileName(dto.Title)));
        candidates.Add(Path.Combine(MediaRoot, safeTitle));
        candidates.Add(Path.Combine(MediaRoot, SanitizeFileName(dto.Title)));

        return candidates.FirstOrDefault(Directory.Exists);
    }

    private string MapRemotePathToLocal(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;

        if (File.Exists(path) || Directory.Exists(path))
            return path;

        var normalized = path.Replace('\\', '/');

        // Transmission default download directories
        if (normalized.Equals("/downloads", StringComparison.OrdinalIgnoreCase) || normalized.Equals("/downloads/", StringComparison.OrdinalIgnoreCase))
        {
            return MediaRoot;
        }
        if (normalized.StartsWith("/downloads/", StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(MediaRoot, normalized.Substring(11).TrimStart('/'));
        }

        // Radarr default movie directories
        if (normalized.Equals("/movies", StringComparison.OrdinalIgnoreCase) || normalized.Equals("/movies/", StringComparison.OrdinalIgnoreCase))
        {
            return MediaRoot;
        }
        if (normalized.StartsWith("/movies/", StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(MediaRoot, normalized.Substring(8).TrimStart('/'));
        }

        // Host volume mounts (e.g. /srv/mytube/videos)
        if (normalized.Equals("/srv/mytube/videos", StringComparison.OrdinalIgnoreCase) || normalized.Equals("/srv/mytube/videos/", StringComparison.OrdinalIgnoreCase))
        {
            return MediaRoot;
        }
        if (normalized.StartsWith("/srv/mytube/videos/", StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(MediaRoot, normalized.Substring(19).TrimStart('/'));
        }

        return Path.Combine(MediaRoot, Path.GetFileName(path));
    }

    public string ResolvePhysicalPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;

        if (File.Exists(path) || Directory.Exists(path)) return path;

        var mapped = MapRemotePathToLocal(path);
        if (File.Exists(mapped) || Directory.Exists(mapped)) return mapped;

        var cleanName = Path.GetFileName(path.TrimEnd('/', '\\'));
        if (!string.IsNullOrWhiteSpace(cleanName))
        {
            var candidates = new List<string>
            {
                Path.Combine(NonDeletableRadarrFolder, cleanName),
                Path.Combine(MediaRoot, cleanName),
                Path.Combine(MediaRoot, "Downloads", cleanName),
                Path.Combine(MediaRoot, "complete", cleanName),
                Path.Combine(MediaRoot, "incomplete", cleanName)
            };

            foreach (var cand in candidates)
            {
                if (File.Exists(cand) || Directory.Exists(cand)) return cand;
                if (File.Exists(cand + ".part")) return cand + ".part";
            }

            try
            {
                if (Directory.Exists(MediaRoot))
                {
                    // Check directories
                    var foundDir = Directory.EnumerateDirectories(MediaRoot, "*", SearchOption.AllDirectories)
                        .FirstOrDefault(d => string.Equals(Path.GetFileName(d), cleanName, StringComparison.OrdinalIgnoreCase));
                    if (foundDir != null) return foundDir;

                    // Check exact file match
                    var foundFile = Directory.EnumerateFiles(MediaRoot, "*", SearchOption.AllDirectories)
                        .FirstOrDefault(f => string.Equals(Path.GetFileName(f), cleanName, StringComparison.OrdinalIgnoreCase));
                    if (foundFile != null) return foundFile;

                    // Check .part file match
                    var foundPartFile = Directory.EnumerateFiles(MediaRoot, "*", SearchOption.AllDirectories)
                        .FirstOrDefault(f => string.Equals(Path.GetFileName(f), cleanName + ".part", StringComparison.OrdinalIgnoreCase));
                    if (foundPartFile != null) return foundPartFile;

                    // Check file match without extension (e.g. torrent title without .mkv)
                    var targetWithoutExt = Path.GetFileNameWithoutExtension(cleanName);
                    if (!string.IsNullOrWhiteSpace(targetWithoutExt))
                    {
                        var foundNoExt = Directory.EnumerateFiles(MediaRoot, "*", SearchOption.AllDirectories)
                            .FirstOrDefault(f => string.Equals(Path.GetFileNameWithoutExtension(f), targetWithoutExt, StringComparison.OrdinalIgnoreCase));
                        if (foundNoExt != null) return foundNoExt;
                    }
                }
            }
            catch { }
        }

        return path;
    }

    private string ResolvePhysicalFilePath(string filePath)
    {
        var resolved = ResolvePhysicalPath(filePath);
        if (File.Exists(resolved)) return resolved;
        return filePath;
    }


    private static void TryCreateFileLink(string sourceFile, string targetLink)
    {
        try
        {
            if (File.Exists(targetLink)) return;
            var dir = Path.GetDirectoryName(targetLink);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            try
            {
                File.CreateSymbolicLink(targetLink, sourceFile);
                return;
            }
            catch { }

            try
            {
                File.Copy(sourceFile, targetLink, overwrite: true);
            }
            catch { }
        }
        catch { }
    }

    private async Task DownloadRemoteImageToFileAsync(string imageUrl, string localPath)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var resp = await client.GetAsync(imageUrl);
            if (resp.IsSuccessStatusCode)
            {
                var bytes = await resp.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(localPath, bytes);
            }
        }
        catch { }
    }

    private static string GetMimeType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".mp4" => "video/mp4",
            ".mkv" => "video/x-matroska",
            ".webm" => "video/webm",
            ".avi" => "video/x-msvideo",
            ".mov" => "video/quicktime",
            ".m4v" => "video/x-m4v",
            ".pdf" => "application/pdf",
            ".txt" or ".nfo" or ".log" => "text/plain",
            ".srt" or ".vtt" or ".sub" or ".ass" => "text/plain",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".mp3" => "audio/mpeg",
            ".m4a" => "audio/mp4",
            ".flac" => "audio/flac",
            ".wav" => "audio/wav",
            ".zip" => "application/zip",
            ".rar" => "application/x-rar-compressed",
            _ => "application/octet-stream"
        };
    }

    private static string ClassifyFileType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".mp4" or ".mkv" or ".webm" or ".avi" or ".mov" or ".m4v" or ".flv" => "video",
            ".pdf" or ".epub" or ".doc" or ".docx" => "pdf",
            ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" or ".bmp" => "image",
            ".srt" or ".vtt" or ".ass" or ".sub" => "subtitle",
            ".nfo" or ".txt" or ".log" => "document",
            ".mp3" or ".m4a" or ".flac" or ".wav" => "audio",
            _ => "other"
        };
    }

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }

    private static string SanitizeFileName(string name)
    {
        return SafePathHelper.SanitizeFileName(name, SafePathHelper.DefaultMaxStemBytes, "movie");
    }

    private async Task EnrichWithHomelabAvailabilityAsync(List<RadarrMovieDto> movies, CancellationToken cancellationToken)
    {
        if (movies == null || movies.Count == 0) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<VideoDbContext>();

            var localVideos = await dbContext.Videos
                .AsNoTracking()
                .Select(v => new { v.Id, v.DisplayTitle, v.FileName, v.FilePath })
                .ToListAsync(cancellationToken);

            foreach (var movie in movies)
            {
                // If Radarr reports hasFile: true, it is available in the media directory
                if (movie.HasFile)
                {
                    movie.IsAvailableInHomelab = true;
                }

                // Cross-reference with MyTube database scanned videos
                var normalizedMovieTitle = NormalizeTitle(movie.Title);
                if (string.IsNullOrWhiteSpace(normalizedMovieTitle)) continue;

                var matchedVideo = localVideos.FirstOrDefault(v =>
                {
                    var normTitle = NormalizeTitle(v.DisplayTitle);
                    var normFile = NormalizeTitle(v.FileName);

                    // Check exact or contains match
                    if (normTitle == normalizedMovieTitle || normFile.Contains(normalizedMovieTitle))
                        return true;

                    // If movie year is present, check title + year
                    if (movie.Year > 0 && normTitle.Contains(normalizedMovieTitle) && normTitle.Contains(movie.Year.ToString()))
                        return true;

                    return false;
                });

                if (matchedVideo != null)
                {
                    movie.IsAvailableInHomelab = true;
                    movie.LocalVideoId = matchedVideo.Id;
                    movie.LocalVideoTitle = matchedVideo.DisplayTitle;
                    movie.LocalVideoPath = matchedVideo.FilePath;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enrich movies with homelab local video availability.");
        }
    }

    private static string NormalizeTitle(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var clean = Regex.Replace(input.ToLowerInvariant(), @"[^\w\d\s]", " ");
        return Regex.Replace(clean, @"\s+", " ").Trim();
    }
}
