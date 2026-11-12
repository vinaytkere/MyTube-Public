using System;
using System.Text.RegularExpressions;

namespace MyTube.Backend.Application.Media;

public static class MediaPortalDetector
{
    private static readonly Regex YouTubeRegex = new(
        @"(?:https?:\/\/)?(?:www\.|m\.)?(?:youtube\.com|youtu\.be)\/(?:watch\?v=|embed\/|v\/|shorts\/)?([\w-]{11})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex FacebookRegex = new(
        @"(?:https?:\/\/)?(?:www\.|m\.|web\.)?(?:facebook\.com|fb\.watch|fb\.com)\/",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex InstagramRegex = new(
        @"(?:https?:\/\/)?(?:www\.)?(?:instagram\.com|instagr\.am)\/(?:p|reel|tv)\/([\w-]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TikTokRegex = new(
        @"(?:https?:\/\/)?(?:www\.|vm\.)?tiktok\.com\/",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TwitterRegex = new(
        @"(?:https?:\/\/)?(?:www\.)?(?:twitter\.com|x\.com)\/",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex RedditRegex = new(
        @"(?:https?:\/\/)?(?:www\.)?reddit\.com\/r\/",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex VimeoRegex = new(
        @"(?:https?:\/\/)?(?:www\.)?vimeo\.com\/",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SoundCloudRegex = new(
        @"(?:https?:\/\/)?(?:www\.)?soundcloud\.com\/",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string DetectPortal(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "Generic";

        if (YouTubeRegex.IsMatch(url)) return "YouTube";
        if (FacebookRegex.IsMatch(url)) return "Facebook";
        if (InstagramRegex.IsMatch(url)) return "Instagram";
        if (TikTokRegex.IsMatch(url)) return "TikTok";
        if (TwitterRegex.IsMatch(url)) return "Twitter";
        if (RedditRegex.IsMatch(url)) return "Reddit";
        if (VimeoRegex.IsMatch(url)) return "Vimeo";
        if (SoundCloudRegex.IsMatch(url)) return "SoundCloud";

        return "Generic";
    }

    public static bool IsPlaylistUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        if (url.Contains("youtube.com/playlist", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("youtube.com/watch", StringComparison.OrdinalIgnoreCase) && url.Contains("list=", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("youtu.be", StringComparison.OrdinalIgnoreCase) && url.Contains("list=", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (url.Contains("soundcloud.com", StringComparison.OrdinalIgnoreCase) && url.Contains("/sets/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    public static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;

        var trimmed = url.Trim();

        // Strip extraneous tracking query parameters while preserving core media IDs
        try
        {
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            {
                var builder = new UriBuilder(uri);
                var query = System.Web.HttpUtility.ParseQueryString(builder.Query);

                // Common marketing/tracking params to strip
                string[] paramsToRemove = { "fbclid", "igsh", "utm_source", "utm_medium", "utm_campaign", "utm_term", "utm_content", "si", "feature" };
                foreach (var param in paramsToRemove)
                {
                    query.Remove(param);
                }

                builder.Query = query.ToString();
                return builder.Uri.ToString();
            }
        }
        catch
        {
            // Fall back to original trimmed string if URI parsing fails
        }

        return trimmed;
    }
}
