using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Configuration;
using MyTube.Backend.Application;

namespace MyTube.Backend.Infrastructure.Media;

public static class YtDlpBypassHelper
{
    public const string DefaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
    public const string DefaultExtractorArgs = "";

    /// <summary>
    /// Applies geo-bypass, client emulation, IPv4 forcing, proxies, cookies, and timeout arguments to yt-dlp arguments list.
    /// </summary>
    public static void ApplyRestrictionBypassArgs(
        List<string> args,
        DownloaderSettingsDto? settings = null,
        IConfiguration? config = null,
        string? mediaRoot = null,
        bool includeProxy = true,
        string? overrideExtractorArgs = null,
        bool includeUserAgent = true)
    {
        // 1. Geo-restriction bypass (--geo-bypass and --xff)
        var geoBypass = settings?.GeoBypass
            ?? (config?["Media:GeoBypass"] is null || bool.Parse(config["Media:GeoBypass"]!));

        if (geoBypass)
        {
            if (!args.Contains("--geo-bypass"))
            {
                args.Add("--geo-bypass");
            }

            var xff = settings?.Xff ?? config?["Media:Xff"] ?? "default";
            if (!string.IsNullOrWhiteSpace(xff) && !args.Contains("--xff"))
            {
                args.Add("--xff");
                args.Add(xff.Trim());
            }
        }

        // 2 & 3. Proxy handling (if enabled)
        if (includeProxy)
        {
            var geoVerificationProxy = settings?.GeoVerificationProxy ?? config?["Media:GeoVerificationProxy"];
            if (!string.IsNullOrWhiteSpace(geoVerificationProxy) && !args.Contains("--geo-verification-proxy"))
            {
                args.Add("--geo-verification-proxy");
                args.Add(geoVerificationProxy.Trim());
            }

            var proxy = settings?.Proxy ?? config?["Media:Proxy"];
            if (!string.IsNullOrWhiteSpace(proxy) && !args.Contains("--proxy"))
            {
                args.Add("--proxy");
                args.Add(proxy.Trim());
            }
        }

        // 4. Force IPv4 (avoids IPv6 rate limits, ISP blocks, and geo-mismatch)
        var forceIpv4 = settings?.ForceIpv4
            ?? (config?["Media:ForceIpv4"] is null || bool.Parse(config["Media:ForceIpv4"]!));

        if (forceIpv4 && !args.Contains("-4") && !args.Contains("--force-ipv4"))
        {
            args.Add("--force-ipv4");
        }

        // 5. Extractor args (only add if explicit and clean of restrictive legacy player_skip or restricted player clients)
        var extractorArgs = overrideExtractorArgs ?? settings?.ExtractorArgs ?? config?["Media:ExtractorArgs"] ?? DefaultExtractorArgs;
        var cleanExtractorArgs = CleanExtractorArgs(extractorArgs);
        if (!string.IsNullOrWhiteSpace(cleanExtractorArgs) && !args.Contains("--extractor-args"))
        {
            args.Add("--extractor-args");
            args.Add(cleanExtractorArgs);
        }

        // 6. User Agent & JS runtimes
        if (includeUserAgent && !args.Contains("--user-agent"))
        {
            args.Add("--user-agent");
            args.Add(DefaultUserAgent);
        }

        if (!args.Contains("--js-runtimes"))
        {
            args.Add("--js-runtimes");
            args.Add("node,nodejs,deno");
        }

        // 7. Socket timeout (prevents hanging on geo-blocked endpoints)
        if (!args.Contains("--socket-timeout"))
        {
            args.Add("--socket-timeout");
            args.Add("30");
        }

        // 8. Cookies bypass (removes age gate, bot gate, login restrictions)
        var cookiesPath = ResolveCookiesPath(settings?.CookiesPath ?? config?["Media:CookiesPath"], mediaRoot);
        if (!string.IsNullOrWhiteSpace(cookiesPath) && File.Exists(cookiesPath) && !args.Contains("--cookies"))
        {
            args.Add("--cookies");
            args.Add(cookiesPath);
        }
    }

    /// <summary>
    /// Searches for cookies.txt across configured path, base directory, working directory, and media root.
    /// </summary>
    public static string? ResolveCookiesPath(string? customPath, string? mediaRoot = null)
    {
        if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
        {
            return Path.GetFullPath(customPath);
        }

        var candidatePaths = new List<string>();
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            candidatePaths.Add(customPath);
        }

        candidatePaths.Add(Path.Combine(AppContext.BaseDirectory, "cookies.txt"));
        candidatePaths.Add(Path.Combine(Directory.GetCurrentDirectory(), "cookies.txt"));

        if (!string.IsNullOrWhiteSpace(mediaRoot))
        {
            candidatePaths.Add(Path.Combine(mediaRoot, "cookies.txt"));
        }

        candidatePaths.Add("/media/cookies.txt");
        candidatePaths.Add("/cache/cookies.txt");

        foreach (var p in candidatePaths)
        {
            try
            {
                if (File.Exists(p))
                {
                    return Path.GetFullPath(p);
                }
            }
            catch { }
        }

        return null;
    }

    /// <summary>
    /// Strips restrictive player clients (e.g. android/ios) and legacy skips that prevent YouTube from exposing HD/4K resolutions.
    /// </summary>
    public static string CleanExtractorArgs(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";

        var cleaned = raw
            .Replace("player_skip=configs,webpage", "", StringComparison.OrdinalIgnoreCase)
            .Replace("player_skip=configs", "", StringComparison.OrdinalIgnoreCase)
            .Replace("player_skip=webpage", "", StringComparison.OrdinalIgnoreCase)
            .Replace("player_client=ios,android,web", "", StringComparison.OrdinalIgnoreCase)
            .Replace("player_client=android,web", "", StringComparison.OrdinalIgnoreCase)
            .Replace("player_client=ios,android", "", StringComparison.OrdinalIgnoreCase)
            .Replace("player_client=android", "", StringComparison.OrdinalIgnoreCase)
            .Replace("player_client=ios", "", StringComparison.OrdinalIgnoreCase)
            .Trim(';', ' ', ',', ':');

        // If nothing of substance remains (e.g. "youtube:" or "youtube"), return empty
        if (cleaned.Equals("youtube", StringComparison.OrdinalIgnoreCase) ||
            cleaned.Equals("youtube:", StringComparison.OrdinalIgnoreCase))
        {
            return "";
        }

        return cleaned;
    }
}
