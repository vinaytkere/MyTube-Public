using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application;

namespace MyTube.Backend.Infrastructure.Media;

public class SourceMetadataService : ISourceMetadataService
{
    private readonly IMediaToolRunner _toolRunner;
    private readonly ILogger<SourceMetadataService> _logger;
    private readonly IDownloaderSettingsStore? _settingsStore;
    private readonly IConfiguration? _configuration;

    public SourceMetadataService(
        IMediaToolRunner toolRunner, 
        ILogger<SourceMetadataService> logger,
        IDownloaderSettingsStore? settingsStore = null,
        IConfiguration? configuration = null)
    {
        _toolRunner = toolRunner;
        _logger = logger;
        _settingsStore = settingsStore;
        _configuration = configuration;
    }

    public async Task<VideoProbeResultDto> ProbeRemoteUrlAsync(string url, CancellationToken token = default)
    {
        var defaultQualities = new List<string>
        {
            "Top Quality Available (Default)",
            "2160p (4K UHD)",
            "1440p (2K QHD)",
            "1080p (Full HD)",
            "720p (HD)",
            "480p (Standard)",
            "360p (Low)"
        };

        var result = new VideoProbeResultDto { Qualities = defaultQualities };
        if (string.IsNullOrWhiteSpace(url)) return result;

        var swProbe = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("[BREAKPOINT:SourceMetadata:START] Probing remote URL {Url}", url);

        try
        {
            var userAgent = YtDlpBypassHelper.DefaultUserAgent;
            var referer = (url.Contains("facebook.com", StringComparison.OrdinalIgnoreCase) || url.Contains("fb.watch", StringComparison.OrdinalIgnoreCase))
                ? "https://www.facebook.com/"
                : "";

            var args = new List<string>
            {
                "-J",
                "--no-playlist",
                "--no-check-certificates",
                "--no-warnings",
                "--js-runtimes", "node",
                "--user-agent", userAgent
            };

            if (!string.IsNullOrEmpty(referer))
            {
                args.Add("--referer");
                args.Add(referer);
            }

            YtDlpBypassHelper.ApplyRestrictionBypassArgs(args, _settingsStore?.GetSettings(), _configuration);

            args.Add(url);

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            linkedCts.CancelAfter(TimeSpan.FromSeconds(60));

            var swYt = System.Diagnostics.Stopwatch.StartNew();
            var toolRes = await _toolRunner.RunToolAsync("yt-dlp", args, linkedCts.Token);
            swYt.Stop();
            _logger.LogInformation("[BREAKPOINT:SourceMetadata:YtDlpExecution] Probing tool exited in {ElapsedMs}ms with Code {Code}",
                swYt.ElapsedMilliseconds, toolRes.ExitCode);

            if (toolRes.ExitCode != 0)
            {
                var settings = _settingsStore?.GetSettings();
                var hasProxy = !string.IsNullOrWhiteSpace(settings?.Proxy) || !string.IsNullOrWhiteSpace(_configuration?["Media:Proxy"]);
                if (hasProxy)
                {
                    _logger.LogWarning("yt-dlp probe failed with proxy ({Code}). Retrying directly without proxy for {Url}", toolRes.ExitCode, url);
                    var directArgs = new List<string>
                    {
                        "-J",
                        "--no-playlist",
                        "--no-check-certificates",
                        "--no-warnings"
                    };
                    if (!string.IsNullOrEmpty(referer))
                    {
                        directArgs.Add("--referer");
                        directArgs.Add(referer);
                    }
                    YtDlpBypassHelper.ApplyRestrictionBypassArgs(directArgs, settings, _configuration, includeProxy: false);
                    directArgs.Add(url);
                    toolRes = await _toolRunner.RunToolAsync("yt-dlp", directArgs, linkedCts.Token);
                }
            }

            var stdout = toolRes.StandardOutput?.Trim();
            if (!string.IsNullOrWhiteSpace(stdout))
            {
                var jsonStart = stdout.IndexOf('{');
                var jsonEnd = stdout.LastIndexOf('}');
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var jsonStr = stdout.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    try
                    {
                        using var doc = JsonDocument.Parse(jsonStr);
                        var root = doc.RootElement;

                if (root.TryGetProperty("title", out var titleEl) && titleEl.ValueKind == JsonValueKind.String)
                {
                    result.Title = titleEl.GetString() ?? "";
                }

                if (root.TryGetProperty("duration", out var durEl) && durEl.TryGetDouble(out var dur))
                {
                    result.DurationSeconds = dur;
                    var ts = TimeSpan.FromSeconds(dur);
                    result.FormattedDuration = ts.TotalHours >= 1
                        ? string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}:{2:D2}", (int)ts.TotalHours, ts.Minutes, ts.Seconds)
                        : string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}", ts.Minutes, ts.Seconds);
                }

                if (root.TryGetProperty("thumbnail", out var thumbEl) && thumbEl.ValueKind == JsonValueKind.String)
                {
                    result.ThumbnailUrl = thumbEl.GetString();
                }

                if (root.TryGetProperty("description", out var descEl) && descEl.ValueKind == JsonValueKind.String)
                {
                    result.Description = descEl.GetString();
                }

                if (root.TryGetProperty("uploader", out var uploaderEl) && uploaderEl.ValueKind == JsonValueKind.String)
                {
                    result.Uploader = uploaderEl.GetString();
                }
                else if (root.TryGetProperty("channel", out var chanEl) && chanEl.ValueKind == JsonValueKind.String)
                {
                    result.Uploader = chanEl.GetString();
                }

                if (root.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tag in tagsEl.EnumerateArray())
                    {
                        var t = tag.GetString();
                        if (!string.IsNullOrWhiteSpace(t)) result.Tags.Add(t);
                    }
                }

                var heights = new HashSet<int>();
                var audioLangs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var audioTrackDtos = new List<MediaTrackInfoDto>();

                if (root.TryGetProperty("formats", out var formatsEl) && formatsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var fmt in formatsEl.EnumerateArray())
                    {
                        if (fmt.TryGetProperty("height", out var hEl) && hEl.ValueKind == JsonValueKind.Number)
                        {
                            var h = hEl.GetInt32();
                            if (h > 0) heights.Add(h);
                        }

                        // Check for audio streams with language tags
                        var isAudioStream = (fmt.TryGetProperty("vcodec", out var vc) && vc.GetString() == "none") ||
                                            (fmt.TryGetProperty("audio_ext", out var ae) && ae.GetString() != "none");

                        if (isAudioStream && fmt.TryGetProperty("language", out var langEl) && langEl.ValueKind == JsonValueKind.String)
                        {
                            var l = langEl.GetString()?.Trim();
                            if (!string.IsNullOrWhiteSpace(l) && !audioLangs.Contains(l))
                            {
                                audioLangs.Add(l);
                                var note = fmt.TryGetProperty("format_note", out var fn) ? fn.GetString() : null;
                                var isOrig = note?.Contains("original", StringComparison.OrdinalIgnoreCase) == true;
                                audioTrackDtos.Add(new MediaTrackInfoDto
                                {
                                    Code = l,
                                    Name = FormatLanguageName(l, isOrig ? "(Original Audio)" : (note != null && note.Contains("dub", StringComparison.OrdinalIgnoreCase) ? "(Dubbed)" : null)),
                                    IsOriginal = isOrig,
                                    IsAutoGenerated = false
                                });
                            }
                        }
                    }
                }

                if (heights.Count > 0)
                {
                    var customQualities = new List<string> { "Top Quality Available (Default)" };
                    foreach (var h in heights.OrderByDescending(h => h))
                    {
                        var label = h switch
                        {
                            >= 2160 => $"{h}p (4K UHD)",
                            >= 1440 => $"{h}p (2K QHD)",
                            >= 1080 => $"{h}p (Full HD)",
                            >= 720 => $"{h}p (HD)",
                            >= 480 => $"{h}p (SD)",
                            _ => $"{h}p"
                        };
                        if (!customQualities.Contains(label))
                        {
                            customQualities.Add(label);
                        }
                    }
                    result.Qualities = customQualities;
                    _logger.LogInformation("[BREAKPOINT:SourceMetadata:END] Probed URL {Url} in {TotalMs}ms: Found {HeightCount} distinct video heights: [{Heights}]. Total qualities exposed: {QualityCount}",
                        url, swProbe.ElapsedMilliseconds, heights.Count, string.Join(", ", heights.OrderByDescending(h => h)), result.Qualities.Count);
                }
                else
                {
                    _logger.LogWarning("[BREAKPOINT:SourceMetadata:END] No video heights found in formats for {Url} (ExitCode {Code}, TotalMs {TotalMs}). Falling back to default qualities. Output preview: {Preview}",
                        url, toolRes.ExitCode, swProbe.ElapsedMilliseconds, stdout.Length > 200 ? stdout[..200] : stdout);
                }

                result.AudioLanguages = audioLangs.OrderBy(l => l).ToList();
                result.AudioTracks = audioTrackDtos.OrderByDescending(t => t.IsOriginal).ThenBy(t => t.Name).ToList();

                var subtitleLangs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var subtitleTrackDtos = new List<MediaTrackInfoDto>();

                if (root.TryGetProperty("subtitles", out var subsEl) && subsEl.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in subsEl.EnumerateObject())
                    {
                        if ((prop.Value.ValueKind == JsonValueKind.Array && prop.Value.GetArrayLength() > 0) ||
                            prop.Value.ValueKind == JsonValueKind.Object)
                        {
                            var code = prop.Name;
                            if (!subtitleLangs.Contains(code))
                            {
                                subtitleLangs.Add(code);
                                var trackName = "";
                                if (prop.Value.ValueKind == JsonValueKind.Array && prop.Value.GetArrayLength() > 0)
                                {
                                    var first = prop.Value[0];
                                    if (first.TryGetProperty("name", out var nEl) && nEl.ValueKind == JsonValueKind.String)
                                    {
                                        trackName = nEl.GetString() ?? "";
                                    }
                                }
                                if (string.IsNullOrWhiteSpace(trackName))
                                {
                                    trackName = FormatLanguageName(code, null);
                                }

                                subtitleTrackDtos.Add(new MediaTrackInfoDto
                                {
                                    Code = code,
                                    Name = trackName,
                                    IsOriginal = true,
                                    IsAutoGenerated = false
                                });
                            }
                        }
                    }
                }

                // If auto-captions exist, include them, prioritizing -orig direct authentic captions
                if (root.TryGetProperty("automatic_captions", out var autoSubsEl) && autoSubsEl.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in autoSubsEl.EnumerateObject())
                    {
                        var name = prop.Name;
                        var isOrig = name.EndsWith("-orig", StringComparison.OrdinalIgnoreCase);

                        // Exclude machine-translated pairs (e.g. "en-es", "ne-es") that trigger YouTube 429
                        if (name.Contains('-') && !isOrig &&
                            !name.Equals("en-US", StringComparison.OrdinalIgnoreCase) && !name.Equals("en-GB", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if ((prop.Value.ValueKind == JsonValueKind.Array && prop.Value.GetArrayLength() > 0) ||
                            prop.Value.ValueKind == JsonValueKind.Object)
                        {
                            var cleanCode = isOrig ? name[..^5] : name;
                            if (!subtitleLangs.Contains(cleanCode))
                            {
                                subtitleLangs.Add(cleanCode);
                            }

                            if (!subtitleTrackDtos.Any(t => t.Code.Equals(name, StringComparison.OrdinalIgnoreCase)))
                            {
                                var trackName = "";
                                if (prop.Value.ValueKind == JsonValueKind.Array && prop.Value.GetArrayLength() > 0)
                                {
                                    var first = prop.Value[0];
                                    if (first.TryGetProperty("name", out var nEl) && nEl.ValueKind == JsonValueKind.String)
                                    {
                                        trackName = nEl.GetString() ?? "";
                                    }
                                }
                                if (string.IsNullOrWhiteSpace(trackName))
                                {
                                    trackName = FormatLanguageName(name, isOrig ? "(Original Captions)" : "(Auto)");
                                }

                                subtitleTrackDtos.Add(new MediaTrackInfoDto
                                {
                                    Code = name,
                                    Name = trackName,
                                    IsOriginal = isOrig,
                                    IsAutoGenerated = true
                                });
                            }
                        }
                    }
                }

                result.SubtitlesAvailable = subtitleLangs.Count > 0;
                result.SubtitleLanguages = subtitleLangs.OrderBy(l => l).ToList();
                result.SubtitleTracks = subtitleTrackDtos.OrderByDescending(t => t.IsOriginal).ThenBy(t => t.Name).ToList();
                    }
                    catch (Exception jsonEx)
                    {
                        _logger.LogWarning(jsonEx, "Failed parsing JSON output from yt-dlp probe for {Url}", url);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed probing remote URL {Url} with yt-dlp", url);
        }

        return result;
    }

    public async Task<List<string>> GetAvailableQualitiesAsync(string url, CancellationToken token = default)
    {
        var probe = await ProbeRemoteUrlAsync(url, token);
        return probe.Qualities ?? new List<string> { "Top Quality Available (Default)" };
    }

    public async Task<PlaylistProbeResultDto> ProbePlaylistAsync(string url, CancellationToken token = default)
    {
        var result = new PlaylistProbeResultDto();
        if (string.IsNullOrWhiteSpace(url)) return result;

        try
        {
            var args = new List<string>
            {
                "-J",
                "--flat-playlist",
                "--no-check-certificates",
                "--no-warnings"
            };

            YtDlpBypassHelper.ApplyRestrictionBypassArgs(args, _settingsStore?.GetSettings(), _configuration);
            args.Add(url);

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            linkedCts.CancelAfter(TimeSpan.FromSeconds(45));

            var toolRes = await _toolRunner.RunToolAsync("yt-dlp", args, linkedCts.Token);
            if (toolRes.ExitCode != 0 || string.IsNullOrWhiteSpace(toolRes.StandardOutput))
            {
                _logger.LogWarning("yt-dlp flat-playlist probe failed for {Url} (ExitCode {Code}): {Err}",
                    url, toolRes.ExitCode, toolRes.StandardError);
                return result;
            }

            var stdout = toolRes.StandardOutput.Trim();
            if (!stdout.StartsWith("{")) return result;

            using var doc = JsonDocument.Parse(stdout);
            var root = doc.RootElement;

            if (root.TryGetProperty("title", out var titleEl) && titleEl.ValueKind == JsonValueKind.String)
            {
                result.Title = titleEl.GetString() ?? "";
            }

            if (root.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
            {
                result.Id = idEl.GetString();
            }

            if (root.TryGetProperty("uploader", out var uploaderEl) && uploaderEl.ValueKind == JsonValueKind.String)
            {
                result.Uploader = uploaderEl.GetString();
            }

            if (root.TryGetProperty("entries", out var entriesEl) && entriesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in entriesEl.EnumerateArray())
                {
                    var itemId = entry.TryGetProperty("id", out var iId) && iId.ValueKind == JsonValueKind.String
                        ? iId.GetString() ?? "" : "";

                    var itemTitle = entry.TryGetProperty("title", out var iTitle) && iTitle.ValueKind == JsonValueKind.String
                        ? iTitle.GetString() ?? "Untitled" : "Untitled";

                    string itemUrl = "";
                    if (entry.TryGetProperty("url", out var iUrl) && iUrl.ValueKind == JsonValueKind.String)
                    {
                        var rawUrl = iUrl.GetString() ?? "";
                        itemUrl = rawUrl.StartsWith("http") ? rawUrl : $"https://www.youtube.com/watch?v={itemId}";
                    }
                    else if (!string.IsNullOrWhiteSpace(itemId))
                    {
                        itemUrl = $"https://www.youtube.com/watch?v={itemId}";
                    }

                    if (string.IsNullOrWhiteSpace(itemUrl)) continue;

                    double? duration = null;
                    string? formattedDur = null;
                    if (entry.TryGetProperty("duration", out var durEl) && durEl.TryGetDouble(out var dVal) && dVal > 0)
                    {
                        duration = dVal;
                        var ts = TimeSpan.FromSeconds(dVal);
                        formattedDur = ts.TotalHours >= 1
                            ? string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}:{2:D2}", (int)ts.TotalHours, ts.Minutes, ts.Seconds)
                            : string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}", ts.Minutes, ts.Seconds);
                    }

                    result.Items.Add(new PlaylistProbeItemDto
                    {
                        Id = itemId,
                        Title = itemTitle,
                        Url = itemUrl,
                        DurationSeconds = duration,
                        FormattedDuration = formattedDur
                    });
                }
            }
            else
            {
                // Single video fallback if not a playlist
                var singleId = root.TryGetProperty("id", out var sId) && sId.ValueKind == JsonValueKind.String ? sId.GetString() ?? "" : "";
                var singleTitle = result.Title.Length > 0 ? result.Title : "Media";
                result.Items.Add(new PlaylistProbeItemDto
                {
                    Id = singleId,
                    Title = singleTitle,
                    Url = url
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to probe playlist for URL {Url}", url);
        }

        return result;
    }

    private static string FormatLanguageName(string code, string? suffix = null)
    {
        var isOrig = code.EndsWith("-orig", StringComparison.OrdinalIgnoreCase);
        var cleanCode = isOrig ? code[..^5] : code;
        string baseName;
        try
        {
            var culture = CultureInfo.GetCultureInfo(cleanCode);
            baseName = culture.EnglishName;
            var parenIdx = baseName.IndexOf('(');
            if (parenIdx > 0 && cleanCode.Length <= 3) baseName = baseName[..parenIdx].Trim();
        }
        catch
        {
            baseName = cleanCode.ToUpperInvariant();
        }

        if (isOrig)
        {
            return $"{baseName} (Original Captions)";
        }

        return !string.IsNullOrWhiteSpace(suffix) ? $"{baseName} {suffix}" : baseName;
    }
}
