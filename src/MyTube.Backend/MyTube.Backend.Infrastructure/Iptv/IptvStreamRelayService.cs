using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application.Iptv;

namespace MyTube.Backend.Infrastructure.Iptv;

public class IptvStreamRelayService : IIptvStreamRelayService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISsrfValidator _ssrfValidator;
    private readonly ILogger<IptvStreamRelayService> _logger;
    private static readonly Regex KeyUriRegex = new Regex(@"URI=""([^""]+)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public IptvStreamRelayService(
        IHttpClientFactory httpClientFactory,
        ISsrfValidator ssrfValidator,
        ILogger<IptvStreamRelayService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _ssrfValidator = ssrfValidator;
        _logger = logger;
    }

    public async Task<StreamRelayResult> RelayStreamAsync(
        string targetUrl,
        string? userAgent = null,
        string? referrer = null,
        CancellationToken ct = default)
    {
        var ssrfCheck = await _ssrfValidator.ValidateUrlAsync(targetUrl, ct);
        if (!ssrfCheck.IsSafe)
        {
            _logger.LogWarning("SSRF validation blocked stream relay request: {Reason}", ssrfCheck.Reason);
            return new StreamRelayResult(false, 403, "text/plain", ErrorMessage: $"Target URL is blocked: {ssrfCheck.Reason}");
        }

        var client = _httpClientFactory.CreateClient("IptvStreamRelay");
        var request = new HttpRequestMessage(HttpMethod.Get, targetUrl);

        if (!string.IsNullOrWhiteSpace(userAgent))
        {
            request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        }
        else
        {
            request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36");
        }

        if (!string.IsNullOrWhiteSpace(referrer))
        {
            request.Headers.TryAddWithoutValidation("Referer", referrer);
        }

        try
        {
            var currentUrl = targetUrl;
            HttpResponseMessage? response = null;
            const int maxRedirects = 3;

            for (int hop = 0; hop <= maxRedirects; hop++)
            {
                var req = new HttpRequestMessage(HttpMethod.Get, currentUrl);
                if (!string.IsNullOrWhiteSpace(userAgent))
                {
                    req.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                }
                else
                {
                    req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36");
                }

                if (!string.IsNullOrWhiteSpace(referrer))
                {
                    req.Headers.TryAddWithoutValidation("Referer", referrer);
                }

                response = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

                if ((int)response.StatusCode >= 300 && (int)response.StatusCode <= 399 && response.Headers.Location != null)
                {
                    var redirectUri = new Uri(new Uri(currentUrl), response.Headers.Location);
                    currentUrl = redirectUri.ToString();

                    var redirectCheck = await _ssrfValidator.ValidateUrlAsync(currentUrl, ct);
                    if (!redirectCheck.IsSafe)
                    {
                        _logger.LogWarning("SSRF validation blocked redirect to {Url}: {Reason}", currentUrl, redirectCheck.Reason);
                        return new StreamRelayResult(false, 403, "text/plain", ErrorMessage: $"Redirect target blocked by SSRF filter: {redirectCheck.Reason}");
                    }
                    continue;
                }

                break;
            }

            if (response == null || !response.IsSuccessStatusCode)
            {
                var statusCode = response != null ? (int)response.StatusCode : 502;
                var errorText = response != null ? await response.Content.ReadAsStringAsync(ct) : "No response from upstream";
                return new StreamRelayResult(false, statusCode, "text/plain", ErrorMessage: $"Upstream server returned {statusCode}: {errorText}");
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            var uri = new Uri(currentUrl);
            var isM3u8 = currentUrl.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) ||
                         contentType.Contains("mpegurl", StringComparison.OrdinalIgnoreCase) ||
                         contentType.Contains("x-mpegurl", StringComparison.OrdinalIgnoreCase);

            if (isM3u8)
            {
                if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value > 5 * 1024 * 1024)
                {
                    return new StreamRelayResult(false, 413, "text/plain", ErrorMessage: "IPTV manifest exceeds maximum allowed size limit of 5 MB.");
                }

                var manifestBytes = await response.Content.ReadAsByteArrayAsync(ct);
                if (manifestBytes.Length > 5 * 1024 * 1024)
                {
                    return new StreamRelayResult(false, 413, "text/plain", ErrorMessage: "IPTV manifest exceeds maximum allowed size limit of 5 MB.");
                }

                var rawManifest = Encoding.UTF8.GetString(manifestBytes);
                var rewrittenManifest = RewriteManifest(rawManifest, uri);

                return new StreamRelayResult(
                    IsSuccess: true,
                    StatusCode: 200,
                    ContentType: "application/vnd.apple.mpegurl; charset=utf-8",
                    ManifestContent: rewrittenManifest);
            }

            // Binary media segment (.ts, .m4s, .aac, .mp4)
            var stream = await response.Content.ReadAsStreamAsync(ct);
            return new StreamRelayResult(
                IsSuccess: true,
                StatusCode: 200,
                ContentType: contentType,
                StreamBody: stream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error relaying IPTV stream {Url}", targetUrl);
            return new StreamRelayResult(false, 502, "text/plain", ErrorMessage: $"Failed to relay stream: {ex.Message}");
        }
    }

    public string RewriteManifest(string manifest, Uri baseUri)
    {
        var lines = manifest.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var sb = new StringBuilder();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                sb.AppendLine(line);
                continue;
            }

            if (trimmed.StartsWith("#"))
            {
                if (trimmed.Contains("URI=", StringComparison.OrdinalIgnoreCase))
                {
                    var rewrittenLine = KeyUriRegex.Replace(trimmed, match =>
                    {
                        var keyUri = match.Groups[1].Value;
                        if (Uri.TryCreate(baseUri, keyUri, out var resolvedKeyUri))
                        {
                            return $"URI=\"/api/iptv/stream-relay?url={Uri.EscapeDataString(resolvedKeyUri.ToString())}\"";
                        }
                        return match.Value;
                    });
                    sb.AppendLine(rewrittenLine);
                }
                else
                {
                    sb.AppendLine(line);
                }
            }
            else
            {
                // URI line for child playlist or media segment
                if (Uri.TryCreate(baseUri, trimmed, out var resolvedUri))
                {
                    sb.AppendLine($"/api/iptv/stream-relay?url={Uri.EscapeDataString(resolvedUri.ToString())}");
                }
                else
                {
                    sb.AppendLine(line);
                }
            }
        }

        return sb.ToString();
    }
}

