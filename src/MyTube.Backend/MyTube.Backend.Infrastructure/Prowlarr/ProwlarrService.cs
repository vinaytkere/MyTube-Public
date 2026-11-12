using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application.Prowlarr;
using MyTube.Backend.Application.Security;

namespace MyTube.Backend.Infrastructure.Prowlarr;

public class ProwlarrService : IProwlarrService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ISecretManagementService _secretService;
    private readonly ILogger<ProwlarrService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly ConcurrentDictionary<string, string> _torrentTitleCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<int, DateTime> _lastKickstartTimes = new();
    private bool _sessionSettingsOptimized;
    private string? _transmissionSessionId;
    private string? _detectedApiKey;
    private string? _activeBaseUrl;

    public static readonly string[] PublicTier1Trackers = new[]
    {
        "http://tracker.opentrackr.org:1337/announce",
        "udp://tracker.opentrackr.org:1337/announce",
        "udp://open.stealth.si:80/announce",
        "udp://tracker.torrent.eu.org:451/announce",
        "udp://tracker.bittor.pw:1337/announce",
        "udp://tracker.moeking.me:6969/announce",
        "udp://explodie.org:6969/announce",
        "udp://tracker.openbittorrent.com:6969/announce",
        "udp://opentracker.i2p.rocks:6969/announce",
        "udp://tracker.tiny-vps.com:6969/announce",
        "udp://open.demonii.com:1337/announce",
        "http://tracker.openbittorrent.com:80/announce",
        "udp://exodus.desync.com:6969/announce",
        "udp://tracker.theoks.net:6969/announce",
        "udp://movies.zsw.ca:6969/announce",
        "udp://p4p.arenabg.com:1337/announce"
    };

    public string? ResolveCachedTitle(string? hashOrName)
    {
        if (string.IsNullOrWhiteSpace(hashOrName)) return null;
        return _torrentTitleCache.TryGetValue(hashOrName.Trim(), out var title) ? title : null;
    }

    public static string EnrichMagnetLink(string magnetUri, string? displayTitle = null)
    {
        if (string.IsNullOrWhiteSpace(magnetUri) || !magnetUri.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
            return magnetUri;

        var sb = new StringBuilder(magnetUri);

        // 1. Add Display Name (&dn=) if missing and title provided
        if (!magnetUri.Contains("&dn=", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(displayTitle))
        {
            sb.Append("&dn=").Append(Uri.EscapeDataString(displayTitle.Trim()));
        }

        // 2. Add high-performance public tier-1 trackers if not already present
        foreach (var tracker in PublicTier1Trackers)
        {
            var escaped = Uri.EscapeDataString(tracker);
            if (!magnetUri.Contains(tracker, StringComparison.OrdinalIgnoreCase) && 
                !magnetUri.Contains(escaped, StringComparison.OrdinalIgnoreCase))
            {
                sb.Append("&tr=").Append(escaped);
            }
        }

        return sb.ToString();
    }

    private static bool IsHexHash(string? str)
    {
        if (string.IsNullOrWhiteSpace(str) || (str.Length != 40 && str.Length != 32))
            return false;
        return str.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
    }

    public ProwlarrService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ISecretManagementService secretService,
        ILogger<ProwlarrService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _secretService = secretService;
        _logger = logger;

        EnsureApiKeyLoaded();
    }

    private string ProwlarrBaseUrl
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_activeBaseUrl)) return _activeBaseUrl;
            return (_configuration["Prowlarr:BaseUrl"] ?? "http://prowlarr:9696").TrimEnd('/');
        }
    }

    private string ProwlarrApiKey
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_detectedApiKey)) return _detectedApiKey;
            EnsureApiKeyLoaded();
            if (!string.IsNullOrWhiteSpace(_detectedApiKey)) return _detectedApiKey;
            return _secretService.GetSecret("Prowlarr:ApiKey") ?? string.Empty;
        }
    }

    private void EnsureApiKeyLoaded(bool force = false)
    {
        if (!force && !string.IsNullOrWhiteSpace(_detectedApiKey)) return;

        var candidates = new[]
        {
            "/prowlarr-config/config.xml",
            "/config/config.xml",
            Path.Combine(AppContext.BaseDirectory, "prowlarr-config", "config.xml")
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
                        _detectedApiKey = match.Groups[1].Value.Trim();
                        _logger.LogInformation("Auto-detected Prowlarr API key from {Path}", path);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not read Prowlarr config from {Path}", path);
            }
        }
    }

    private async Task<string?> ProbeHealthyBaseUrlAsync(string apiKey, CancellationToken cancellationToken)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(_activeBaseUrl))
            candidates.Add(_activeBaseUrl);

        var configured = _configuration["Prowlarr:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(configured))
            candidates.Add(configured);

        candidates.Add("http://prowlarr:9696");
        candidates.Add("http://localhost:9696");
        candidates.Add("http://127.0.0.1:9696");
        candidates.Add("http://host.docker.internal:9696");
        candidates.Add("http://192.168.1.16:9696");

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

                var resp = await client.GetAsync("/api/v1/system/status", cancellationToken);
                if (resp.IsSuccessStatusCode || resp.StatusCode == HttpStatusCode.Unauthorized)
                {
                    if (!string.Equals(_activeBaseUrl, candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Auto-switching Prowlarr BaseUrl from {Old} to {New}", _activeBaseUrl, candidate);
                        _activeBaseUrl = candidate.TrimEnd('/');
                    }
                    return _activeBaseUrl;
                }
            }
            catch
            {
                // Continue probing
            }
        }

        return null;
    }

    private string TransmissionBaseUrl =>
        (_configuration["Transmission:BaseUrl"] ?? "http://gluetun:9091").TrimEnd('/');

    private string TransmissionUser =>
        _configuration["Transmission:Username"] ?? "admin";

    private string TransmissionPass =>
        _configuration["Transmission:Password"] ?? "admin";

    public async Task<List<ProwlarrSearchResultDto>> SearchAsync(string query, string? category = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<ProwlarrSearchResultDto>();

        try
        {
            EnsureApiKeyLoaded();
            var key = ProwlarrApiKey;
            var baseUrl = ProwlarrBaseUrl;

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(45);
            client.DefaultRequestHeaders.Add("X-Api-Key", key);

            var trimmedQuery = query.Trim();
            var effectiveQuery = trimmedQuery;

            // Map generic category search terms to popular/trending release tags so indexers return top releases instead of 0
            var lowerQuery = trimmedQuery.ToLowerInvariant();
            if (lowerQuery is "movies" or "movie" or "films" or "popular movies" or "trending movies")
            {
                effectiveQuery = "2024 1080p";
            }
            else if (lowerQuery is "software" or "apps" or "programs")
            {
                effectiveQuery = "Windows 2024";
            }
            else if (lowerQuery is "books" or "ebooks" or "pdfs")
            {
                effectiveQuery = "Python";
            }
            else if (lowerQuery is "music" or "songs" or "audio")
            {
                effectiveQuery = "FLAC 2024";
            }
            else if (lowerQuery is "tv" or "tv shows" or "series" or "shows")
            {
                effectiveQuery = "S01 1080p";
            }

            var catQuery = BuildCategoryQuery(category);
            var url = $"{baseUrl}/api/v1/search?query={Uri.EscapeDataString(effectiveQuery)}&type=search{catQuery}";
            _logger.LogInformation("Searching Prowlarr indexers for query: {Query} (Effective: {EffectiveQuery}, Category: {Category}) at {BaseUrl}", query, effectiveQuery, category ?? "All", baseUrl);

            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(url, cancellationToken);
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Prowlarr returned 401 Unauthorized with configured API key. Attempting to reload key from disk...");
                    EnsureApiKeyLoaded(force: true);
                    key = ProwlarrApiKey;
                    using var retryClient = _httpClientFactory.CreateClient();
                    retryClient.Timeout = TimeSpan.FromSeconds(45);
                    retryClient.DefaultRequestHeaders.Add("X-Api-Key", key);
                    response = await retryClient.GetAsync(url, cancellationToken);
                }
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Initial Prowlarr search failed at {BaseUrl}. Probing healthy endpoint...", baseUrl);
                var recovered = await ProbeHealthyBaseUrlAsync(key, cancellationToken);
                if (!string.IsNullOrWhiteSpace(recovered))
                {
                    baseUrl = recovered;
                    url = $"{baseUrl}/api/v1/search?query={Uri.EscapeDataString(query.Trim())}&type=search{catQuery}";
                    using var fbClient = _httpClientFactory.CreateClient();
                    fbClient.Timeout = TimeSpan.FromSeconds(45);
                    fbClient.DefaultRequestHeaders.Add("X-Api-Key", key);
                    response = await fbClient.GetAsync(url, cancellationToken);
                    response.EnsureSuccessStatusCode();
                }
                else
                {
                    throw;
                }
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var results = new List<ProwlarrSearchResultDto>();

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var dto = new ProwlarrSearchResultDto
                    {
                        Guid = el.TryGetProperty("guid", out var g) ? g.GetString() ?? string.Empty : string.Empty,
                        Title = el.TryGetProperty("title", out var t) ? t.GetString() ?? string.Empty : string.Empty,
                        DownloadUrl = el.TryGetProperty("downloadUrl", out var du) ? du.GetString() : null,
                        MagnetUrl = el.TryGetProperty("magnetUrl", out var mu) ? mu.GetString() : null,
                        InfoUrl = el.TryGetProperty("infoUrl", out var iu) ? iu.GetString() : null,
                        Size = el.TryGetProperty("size", out var s) && s.TryGetInt64(out var sz) ? sz : 0,
                        Indexer = el.TryGetProperty("indexer", out var idx) ? idx.GetString() ?? string.Empty : string.Empty,
                        IndexerId = el.TryGetProperty("indexerId", out var idxId) && idxId.TryGetInt32(out var idVal) ? idVal : 0,
                        Seeders = el.TryGetProperty("seeders", out var seed) && seed.TryGetInt32(out var seedVal) ? seedVal : null,
                        Leechers = el.TryGetProperty("leechers", out var leech) && leech.TryGetInt32(out var leechVal) ? leechVal : null,
                        Protocol = el.TryGetProperty("protocol", out var pr) ? pr.GetString() ?? "torrent" : "torrent"
                    };

                    if (string.IsNullOrWhiteSpace(dto.MagnetUrl) && dto.Guid.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
                    {
                        dto.MagnetUrl = dto.Guid;
                    }

                    if (string.IsNullOrWhiteSpace(dto.DownloadUrl) && !string.IsNullOrWhiteSpace(dto.MagnetUrl))
                    {
                        dto.DownloadUrl = dto.MagnetUrl;
                    }

                    if (el.TryGetProperty("publishDate", out var pub) && pub.TryGetDateTime(out var dt))
                    {
                        dto.PublishDate = dt;
                    }

                    if (el.TryGetProperty("categories", out var cats) && cats.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var cat in cats.EnumerateArray())
                        {
                            if (cat.TryGetProperty("id", out var cId) && cId.TryGetInt32(out var idNum))
                            {
                                dto.CategoryIds.Add(idNum);
                            }
                            if (cat.TryGetProperty("name", out var cn) && cn.GetString() is { } name)
                            {
                                dto.Categories.Add(name);
                            }
                        }
                    }

                    var (primary, badge) = ClassifyCategory(dto.CategoryIds, dto.Categories, dto.Title);
                    dto.PrimaryCategory = primary;
                    dto.CategoryBadge = badge;

                    // Must have either a valid guid, downloadUrl, or magnetUrl
                    if (!string.IsNullOrWhiteSpace(dto.Guid) || !string.IsNullOrWhiteSpace(dto.DownloadUrl) || !string.IsNullOrWhiteSpace(dto.MagnetUrl))
                    {
                        results.Add(dto);
                    }
                }
            }

            _logger.LogInformation("Prowlarr search for {Query} returned {Count} releases.", query, results.Count);

            // Order by quality score (boosting verified active swarms and penalizing scrapers with fake seeder numbers)
            return results
                .OrderByDescending(CalculateQualityScore)
                .ThenByDescending(r => r.Seeders ?? 0)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying Prowlarr indexers for {Query}", query);
            throw;
        }
    }

    private static int CalculateQualityScore(ProwlarrSearchResultDto r)
    {
        var rawSeeders = r.Seeders ?? 0;
        var indexer = (r.Indexer ?? string.Empty).Trim().ToLowerInvariant();

        // High-trust verified indexers that maintain live, active swarms
        var isVerified = indexer.Contains("pirate") || indexer.Contains("yts") || indexer.Contains("1337x") ||
                         indexer.Contains("eztv") || indexer.Contains("nyaa") || indexer.Contains("galaxy") ||
                         indexer.Contains("tgx");

        // Aggregator scrapers notorious for reporting hardcoded, fake, or stale seeder numbers (e.g., 1999) on dead swarms
        var isScraper = indexer.Contains("torrentdownload") || indexer.Contains("limetorrent") || indexer.Contains("bitsearch");
        if (isScraper && rawSeeders > 50)
        {
            // Cap inflated scraper counts so they do not outrank verified real releases
            rawSeeders = Math.Min(rawSeeders, 10);
        }

        var boost = isVerified ? 500 : 0;
        return rawSeeders + boost;
    }

    private static string BuildCategoryQuery(string? category)
    {
        if (string.IsNullOrWhiteSpace(category) || string.Equals(category, "all", StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        var lower = category.Trim().ToLowerInvariant();
        return lower switch
        {
            "software" or "pc" or "apps" => "&categories=4000&categories=4010&categories=4020&categories=4030&categories=4040&categories=4050&categories=4060&categories=4070",
            "books" or "pdf" or "ebook" => "&categories=7000&categories=7010&categories=7020&categories=7030&categories=7040",
            "music" or "audio" => "&categories=3000&categories=3010&categories=3020&categories=3030&categories=3040&categories=3050",
            "movies" or "movie" => "&categories=2000&categories=2010&categories=2020&categories=2030&categories=2040&categories=2045&categories=2050&categories=2060",
            "tv" or "tvshows" => "&categories=5000&categories=5010&categories=5020&categories=5030&categories=5040&categories=5050",
            "games" => "&categories=1000&categories=1010&categories=1020&categories=1030",
            _ => string.Empty
        };
    }

    private static (string PrimaryCategory, string CategoryBadge) ClassifyCategory(List<int> categoryIds, List<string> categoryNames, string title)
    {
        // Check category IDs first
        foreach (var id in categoryIds)
        {
            if (id >= 4000 && id < 5000) return ("Software", "Software");
            if (id >= 7000 && id < 8000) return ("Books / PDF", "PDF / EBook");
            if (id >= 3000 && id < 4000) return ("Music / Audio", "Music");
            if (id >= 1000 && id < 2000) return ("Games", "Games");
            if (id >= 5000 && id < 6000) return ("TV Shows", "TV");
            if (id >= 2000 && id < 3000) return ("Movies", "Movie");
        }

        // Check category names
        foreach (var name in categoryNames)
        {
            var lower = name.ToLowerInvariant();
            if (lower.Contains("pc") || lower.Contains("software") || lower.Contains("mac") || lower.Contains("windows") || lower.Contains("0day") || lower.Contains("app"))
                return ("Software", "Software");
            if (lower.Contains("book") || lower.Contains("ebook") || lower.Contains("pdf") || lower.Contains("comic") || lower.Contains("magazine"))
                return ("Books / PDF", "PDF / EBook");
            if (lower.Contains("audio") || lower.Contains("music") || lower.Contains("mp3") || lower.Contains("flac") || lower.Contains("lossless"))
                return ("Music / Audio", "Music");
            if (lower.Contains("game") || lower.Contains("console"))
                return ("Games", "Games");
            if (lower.Contains("tv"))
                return ("TV Shows", "TV");
            if (lower.Contains("movie"))
                return ("Movies", "Movie");
        }

        // Fallback: examine title extensions/keywords
        var t = title.ToLowerInvariant();
        if (t.EndsWith(".pdf") || t.EndsWith(".epub") || t.EndsWith(".mobi") || t.Contains("pdf") || t.Contains("ebook") || t.Contains("cookbook"))
            return ("Books / PDF", "PDF / EBook");
        if (t.EndsWith(".exe") || t.EndsWith(".dmg") || t.EndsWith(".iso") || t.EndsWith(".pkg") || t.Contains("portable") || t.Contains("repack") || t.Contains("cracked"))
            return ("Software", "Software");
        if (t.EndsWith(".flac") || t.EndsWith(".mp3") || t.EndsWith(".wav") || t.Contains("discography") || t.Contains("320kbps") || t.Contains("flac"))
            return ("Music / Audio", "Music");
        if (t.Contains("s0") || t.Contains("s1") || t.Contains("s2") || t.Contains("season") || t.Contains("episode") || t.Contains("complete series"))
            return ("TV Shows", "TV");

        return ("Other", "Files");
    }

    public async Task<bool> DownloadAsync(ProwlarrDownloadRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureTransmissionSettingsOptimizedAsync(cancellationToken);

        string? magnetLink = null;
        if (!string.IsNullOrWhiteSpace(request.MagnetUrl) && request.MagnetUrl.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
        {
            magnetLink = request.MagnetUrl;
        }
        else if (!string.IsNullOrWhiteSpace(request.DownloadUrl) && request.DownloadUrl.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
        {
            magnetLink = request.DownloadUrl;
        }
        else if (!string.IsNullOrWhiteSpace(request.Guid) && request.Guid.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
        {
            magnetLink = request.Guid;
        }

        // Identify possible HTTP URL to download .torrent file directly
        string? httpTorrentUrl = null;
        if (!string.IsNullOrWhiteSpace(request.DownloadUrl) && (request.DownloadUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || request.DownloadUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            httpTorrentUrl = request.DownloadUrl;
        }
        else if (!string.IsNullOrWhiteSpace(request.Guid) && (request.Guid.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || request.Guid.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            httpTorrentUrl = request.Guid;
        }

        string? metainfoBase64 = null;

        // Try downloading .torrent file first if HTTP URL is available
        if (!string.IsNullOrWhiteSpace(httpTorrentUrl))
        {
            var targetUrl = httpTorrentUrl;
            if (targetUrl.StartsWith("/"))
            {
                targetUrl = $"{ProwlarrBaseUrl}{targetUrl}";
            }
            else if (targetUrl.Contains("localhost:9696", StringComparison.OrdinalIgnoreCase) ||
                     targetUrl.Contains("127.0.0.1:9696", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(targetUrl);
                targetUrl = $"{ProwlarrBaseUrl}{uri.PathAndQuery}";
            }

            _logger.LogInformation("Resolving .torrent file for release '{Title}' from: {Url}", request.Title, targetUrl);

            try
            {
                var handler = new HttpClientHandler { AllowAutoRedirect = false };
                using var dlClient = new HttpClient(handler);
                dlClient.Timeout = TimeSpan.FromSeconds(25);

                if (targetUrl.StartsWith(ProwlarrBaseUrl, StringComparison.OrdinalIgnoreCase) || targetUrl.Contains("/download", StringComparison.OrdinalIgnoreCase))
                {
                    dlClient.DefaultRequestHeaders.Remove("X-Api-Key");
                    dlClient.DefaultRequestHeaders.Add("X-Api-Key", ProwlarrApiKey);
                }

                var currentUrl = targetUrl;
                var redirectsFollowed = 0;
                while (redirectsFollowed < 5)
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, currentUrl);
                    var resp = await dlClient.SendAsync(req, cancellationToken);

                    // Intercept redirects to magnet: scheme
                    if (resp.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect)
                    {
                        var location = resp.Headers.Location?.ToString();
                        if (!string.IsNullOrWhiteSpace(location))
                        {
                            if (location.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
                            {
                                magnetLink = location;
                                break;
                            }
                            currentUrl = location.StartsWith("/") ? $"{ProwlarrBaseUrl}{location}" : location;
                            redirectsFollowed++;
                            continue;
                        }
                    }

                    if (resp.IsSuccessStatusCode)
                    {
                        var bytes = await resp.Content.ReadAsByteArrayAsync(cancellationToken);
                        var sample = bytes.Length > 0 ? Encoding.UTF8.GetString(bytes.Take(Math.Min(bytes.Length, 64)).ToArray()) : "";
                        if (!sample.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) && !sample.Contains("<html", StringComparison.OrdinalIgnoreCase) && bytes.Length > 20)
                        {
                            metainfoBase64 = Convert.ToBase64String(bytes);
                            _logger.LogInformation("Successfully resolved .torrent file ({Bytes} bytes) for '{Title}'", bytes.Length, request.Title);
                            break;
                        }
                    }
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HTTP torrent resolution failed, falling back to magnet link: {Url}", targetUrl);
            }
        }

        // If no .torrent file obtained, use enriched magnet link
        if (string.IsNullOrWhiteSpace(metainfoBase64))
        {
            if (string.IsNullOrWhiteSpace(magnetLink))
            {
                throw new InvalidOperationException("Failed to acquire a valid torrent file or magnet link for this release.");
            }

            magnetLink = EnrichMagnetLink(magnetLink, request.Title);

            // Cache infohash -> title
            var hashMatch = Regex.Match(magnetLink, @"xt=urn:btih:([a-fA-F0-9]{40}|[a-zA-Z2-7]{32})", RegexOptions.IgnoreCase);
            if (hashMatch.Success && !string.IsNullOrWhiteSpace(request.Title))
            {
                var hash = hashMatch.Groups[1].Value.ToLowerInvariant();
                _torrentTitleCache[hash] = request.Title;
            }
        }

        // 3. Dispatch to Transmission via RPC
        _logger.LogInformation("Sending torrent to Transmission via Gluetun: {Title}", request.Title ?? "Unknown Title");
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(25);

        var authHeader = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{TransmissionUser}:{TransmissionPass}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

        var rpcUrl = $"{TransmissionBaseUrl}/transmission/rpc";

        string payloadJson;
        if (!string.IsNullOrWhiteSpace(metainfoBase64))
        {
            payloadJson = JsonSerializer.Serialize(new
            {
                method = "torrent-add",
                @arguments = new
                {
                    metainfo = metainfoBase64,
                    download_dir = "/downloads",
                    labels = new[] { "radarr", "mytube" }
                }
            }).Replace("download_dir", "download-dir");
        }
        else
        {
            payloadJson = JsonSerializer.Serialize(new
            {
                method = "torrent-add",
                @arguments = new
                {
                    filename = magnetLink,
                    download_dir = "/downloads",
                    labels = new[] { "radarr", "mytube" }
                }
            }).Replace("download_dir", "download-dir");
        }

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var msg = new HttpRequestMessage(HttpMethod.Post, rpcUrl);
                msg.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");

                if (!string.IsNullOrWhiteSpace(_transmissionSessionId))
                {
                    msg.Headers.Add("X-Transmission-Session-Id", _transmissionSessionId);
                }

                var resp = await client.SendAsync(msg, cancellationToken);
                if (resp.StatusCode == HttpStatusCode.Conflict && resp.Headers.TryGetValues("X-Transmission-Session-Id", out var vals))
                {
                    _transmissionSessionId = vals.FirstOrDefault();
                    continue;
                }

                if (resp.IsSuccessStatusCode)
                {
                    var respBody = await resp.Content.ReadAsStringAsync(cancellationToken);
                    using var doc = JsonDocument.Parse(respBody);
                    var resultStr = doc.RootElement.TryGetProperty("result", out var r) ? r.GetString() : null;

                    if (string.Equals(resultStr, "success", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Transmission successfully accepted torrent: {Body}", respBody);
                        return true;
                    }
                    else if (string.Equals(resultStr, "duplicate torrent", StringComparison.OrdinalIgnoreCase) ||
                             (doc.RootElement.TryGetProperty("arguments", out var args) && args.TryGetProperty("torrent-duplicate", out _)))
                    {
                        _logger.LogInformation("Transmission reported torrent is already queued: {Body}", respBody);
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning("Transmission rejected torrent-add: {Result}. Body: {Body}", resultStr, respBody);
                        throw new InvalidOperationException($"Transmission rejected torrent: {resultStr ?? "unknown rejection"}");
                    }
                }

                _logger.LogWarning("Transmission rejected torrent-add with HTTP status {Status}", resp.StatusCode);
            }
            catch (HttpRequestException ex) when (attempt < 2)
            {
                _logger.LogWarning(ex, "Transient connection failure communicating with Transmission (attempt {Attempt}/3). Retrying in 2 seconds...", attempt + 1);
                await Task.Delay(2000, cancellationToken);
            }
        }

        return false;
    }

    public async Task<List<TransmissionTorrentDto>> GetTransmissionTorrentsAsync(CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        var authHeader = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{TransmissionUser}:{TransmissionPass}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

        var rpcUrl = $"{TransmissionBaseUrl}/transmission/rpc";
        var payload = "{\"method\":\"torrent-get\",\"arguments\":{\"fields\":[\"id\",\"name\",\"percentDone\",\"rateDownload\",\"rateUpload\",\"status\",\"sizeWhenDone\",\"leftUntilDone\",\"eta\",\"peersSendingToUs\",\"errorString\",\"downloadDir\",\"files\",\"hashString\",\"metadataPercentComplete\",\"trackers\"]}}";

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var msg = new HttpRequestMessage(HttpMethod.Post, rpcUrl);
            msg.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            if (!string.IsNullOrWhiteSpace(_transmissionSessionId))
            {
                msg.Headers.Add("X-Transmission-Session-Id", _transmissionSessionId);
            }

            var resp = await client.SendAsync(msg, cancellationToken);
            if (resp.StatusCode == HttpStatusCode.Conflict && resp.Headers.TryGetValues("X-Transmission-Session-Id", out var vals))
            {
                _transmissionSessionId = vals.FirstOrDefault();
                continue;
            }

            if (resp.IsSuccessStatusCode)
            {
                var content = await resp.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(content);
                var torrentsList = new List<TransmissionTorrentDto>();
                if (doc.RootElement.TryGetProperty("arguments", out var args) &&
                    args.TryGetProperty("torrents", out var torrentsArray) &&
                    torrentsArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in torrentsArray.EnumerateArray())
                    {
                        var t = new TransmissionTorrentDto
                        {
                            Id = item.TryGetProperty("id", out var id) && id.TryGetInt32(out var idVal) ? idVal : 0,
                            Name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                            PercentDone = item.TryGetProperty("percentDone", out var pd) && pd.TryGetDouble(out var pdVal) ? pdVal : 0.0,
                            RateDownload = item.TryGetProperty("rateDownload", out var rd) && rd.TryGetDouble(out var rdVal) ? rdVal : 0.0,
                            RateUpload = item.TryGetProperty("rateUpload", out var ru) && ru.TryGetDouble(out var ruVal) ? ruVal : 0.0,
                            Status = item.TryGetProperty("status", out var st) && st.TryGetInt32(out var stVal) ? stVal : 0,
                            SizeWhenDone = item.TryGetProperty("sizeWhenDone", out var swd) && swd.TryGetDouble(out var swdVal) ? swdVal : 0.0,
                            LeftUntilDone = item.TryGetProperty("leftUntilDone", out var lud) && lud.TryGetDouble(out var ludVal) ? ludVal : 0.0,
                            Eta = item.TryGetProperty("eta", out var eta) && eta.TryGetInt64(out var etaVal) ? etaVal : 0,
                            PeersSendingToUs = item.TryGetProperty("peersSendingToUs", out var p) && p.TryGetInt32(out var pVal) ? pVal : 0,
                            ErrorString = item.TryGetProperty("errorString", out var es) ? es.GetString() : null,
                            DownloadDir = item.TryGetProperty("downloadDir", out var dd) ? dd.GetString() : null,
                            HashString = item.TryGetProperty("hashString", out var hs) ? hs.GetString() ?? "" : "",
                            MetadataPercentComplete = item.TryGetProperty("metadataPercentComplete", out var mpc) && mpc.TryGetDouble(out var mpcVal) ? mpcVal : 1.0,
                            TrackersCount = item.TryGetProperty("trackers", out var trks) && trks.ValueKind == JsonValueKind.Array ? trks.GetArrayLength() : 0
                        };

                        if (item.TryGetProperty("files", out var fArray) && fArray.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var fEl in fArray.EnumerateArray())
                            {
                                var f = new TransmissionFileDto
                                {
                                    Name = fEl.TryGetProperty("name", out var fn) ? fn.GetString() ?? "" : "",
                                    Length = fEl.TryGetProperty("length", out var fl) && fl.TryGetInt64(out var flVal) ? flVal : 0,
                                    BytesCompleted = fEl.TryGetProperty("bytesCompleted", out var fbc) && fbc.TryGetInt64(out var fbcVal) ? fbcVal : 0
                                };
                                t.Files.Add(f);
                            }
                        }

                        t.FilesCount = t.Files.Count;
                        t.IsDirectory = t.FilesCount > 1 || (!string.IsNullOrEmpty(t.Name) && !Path.HasExtension(t.Name));
                        torrentsList.Add(t);

                        // Auto-kickstart if torrent is stuck waiting for metadata or stalled with 0 peers
                        var isStuckMetadata = (t.MetadataPercentComplete < 1.0 || t.SizeWhenDone == 0 || IsHexHash(t.Name)) && t.Status != 0;
                        var isStalled = t.Status == 4 && t.PeersSendingToUs == 0 && t.PercentDone == 0;

                        if (isStuckMetadata || isStalled)
                        {
                            if (!_lastKickstartTimes.TryGetValue(t.Id, out var lastTime) || DateTime.UtcNow - lastTime > TimeSpan.FromSeconds(60))
                            {
                                _lastKickstartTimes[t.Id] = DateTime.UtcNow;
                                var stuckId = t.Id;
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        await KickstartTransmissionTorrentAsync(stuckId);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "Failed to auto-kickstart stuck torrent {Id}", stuckId);
                                    }
                                });
                            }
                        }
                    }
                }
                return torrentsList;
            }
        }

        return new List<TransmissionTorrentDto>();
    }

    public async Task<bool> KickstartTransmissionTorrentAsync(int torrentId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Kickstarting torrent {Id}: injecting 15+ tier-1 public trackers and re-announcing", torrentId);
        await EnsureTransmissionSettingsOptimizedAsync(cancellationToken);

        var trackerListString = string.Join("\n\n", PublicTier1Trackers);

        // 1. Inject trackers via torrent-set
        var setPayload = JsonSerializer.Serialize(new
        {
            method = "torrent-set",
            @arguments = new
            {
                ids = new[] { torrentId },
                trackerList = trackerListString
            }
        });

        await ExecuteTransmissionTorrentActionAsync("torrent-set", torrentId, cancellationToken, setPayload);

        // 2. Start now (bypasses any queue limits)
        await ExecuteTransmissionTorrentActionAsync("torrent-start-now", torrentId, cancellationToken);

        // 3. Re-announce immediately to all trackers
        await ExecuteTransmissionTorrentActionAsync("torrent-reannounce", torrentId, cancellationToken);

        return true;
    }

    private async Task EnsureTransmissionSettingsOptimizedAsync(CancellationToken cancellationToken = default)
    {
        if (_sessionSettingsOptimized) return;

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                method = "session-set",
                @arguments = new
                {
                    dht_enabled = true,
                    pex_enabled = true,
                    lpd_enabled = true,
                    utp_enabled = true,
                    encryption = "preferred",
                    peer_port = 51413,
                    peer_limit_global = 500,
                    peer_limit_per_torrent = 100,
                    port_forwarding_enabled = false
                }
            }).Replace("_", "-");

            var success = await ExecuteTransmissionSessionActionAsync(payload, cancellationToken);
            if (success)
            {
                _sessionSettingsOptimized = true;
                _logger.LogInformation("Transmission session settings optimized via RPC (dht=true, pex=true, utp=true, encryption=preferred, peer-limit=500)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not optimize Transmission session settings via RPC (will retry)");
        }
    }

    private async Task<bool> ExecuteTransmissionSessionActionAsync(string payload, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        var authHeader = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{TransmissionUser}:{TransmissionPass}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);
        var rpcUrl = $"{TransmissionBaseUrl}/transmission/rpc";

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var msg = new HttpRequestMessage(HttpMethod.Post, rpcUrl);
            msg.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            if (!string.IsNullOrWhiteSpace(_transmissionSessionId))
            {
                msg.Headers.Add("X-Transmission-Session-Id", _transmissionSessionId);
            }

            var resp = await client.SendAsync(msg, cancellationToken);
            if (resp.StatusCode == HttpStatusCode.Conflict && resp.Headers.TryGetValues("X-Transmission-Session-Id", out var vals))
            {
                _transmissionSessionId = vals.FirstOrDefault();
                continue;
            }

            return resp.IsSuccessStatusCode;
        }

        return false;
    }

    public async Task<bool> RemoveTransmissionTorrentAsync(int torrentId, bool deleteData = true, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Removing torrent {Id} from Transmission (deleteData={DeleteData})", torrentId, deleteData);
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(15);
        var authHeader = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{TransmissionUser}:{TransmissionPass}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

        var rpcUrl = $"{TransmissionBaseUrl}/transmission/rpc";
        var payload = $"{{\"method\":\"torrent-remove\",\"arguments\":{{\"ids\":[{torrentId}],\"delete-local-data\":{(deleteData ? "true" : "false")}}}}}";

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var msg = new HttpRequestMessage(HttpMethod.Post, rpcUrl);
            msg.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            if (!string.IsNullOrWhiteSpace(_transmissionSessionId))
            {
                msg.Headers.Add("X-Transmission-Session-Id", _transmissionSessionId);
            }

            var resp = await client.SendAsync(msg, cancellationToken);
            if (resp.StatusCode == HttpStatusCode.Conflict && resp.Headers.TryGetValues("X-Transmission-Session-Id", out var vals))
            {
                _transmissionSessionId = vals.FirstOrDefault();
                continue;
            }

            if (resp.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully removed torrent {Id} from Transmission", torrentId);
                return true;
            }
        }

        return false;
    }

    public async Task<bool> PauseTransmissionTorrentAsync(int torrentId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Pausing torrent {Id} in Transmission", torrentId);
        return await ExecuteTransmissionTorrentActionAsync("torrent-stop", torrentId, cancellationToken);
    }

    public async Task<bool> ResumeTransmissionTorrentAsync(int torrentId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Resuming torrent {Id} in Transmission", torrentId);
        return await ExecuteTransmissionTorrentActionAsync("torrent-start", torrentId, cancellationToken);
    }

    private async Task<bool> ExecuteTransmissionTorrentActionAsync(string method, int torrentId, CancellationToken cancellationToken, string? customPayload = null)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(15);
        var authHeader = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{TransmissionUser}:{TransmissionPass}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

        var rpcUrl = $"{TransmissionBaseUrl}/transmission/rpc";
        var payload = customPayload ?? $"{{\"method\":\"{method}\",\"arguments\":{{\"ids\":[{torrentId}]}}}}";

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var msg = new HttpRequestMessage(HttpMethod.Post, rpcUrl);
            msg.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            if (!string.IsNullOrWhiteSpace(_transmissionSessionId))
            {
                msg.Headers.Add("X-Transmission-Session-Id", _transmissionSessionId);
            }

            var resp = await client.SendAsync(msg, cancellationToken);
            if (resp.StatusCode == HttpStatusCode.Conflict && resp.Headers.TryGetValues("X-Transmission-Session-Id", out var vals))
            {
                _transmissionSessionId = vals.FirstOrDefault();
                continue;
            }

            if (resp.IsSuccessStatusCode)
            {
                _logger.LogInformation("Transmission successfully executed {Method} on torrent {Id}", method, torrentId);
                return true;
            }
        }

        return false;
    }
}
