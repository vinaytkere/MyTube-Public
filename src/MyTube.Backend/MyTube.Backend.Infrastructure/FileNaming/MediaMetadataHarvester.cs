using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application;
using MyTube.Backend.Domain;

namespace MyTube.Backend.Infrastructure.FileNaming;

public class HarvestedMediaEvidence
{
    public string RawFileName { get; set; } = string.Empty;
    public string CleanTitleCandidate { get; set; } = string.Empty;
    public int? ExtractedYear { get; set; }
    public string? ExtractedSeasonEpisode { get; set; } // e.g. "S01E01"
    public string? DetectedLanguage { get; set; } // e.g. "Kannada", "English"
    public string? EmbeddedTitle { get; set; }
    public string? EmbeddedArtist { get; set; }
    public string? EmbeddedAlbum { get; set; }
    public string? EmbeddedGenre { get; set; }
    public string? EmbeddedDate { get; set; }
    public List<string> StreamLanguages { get; set; } = new();
    public double DurationSeconds { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsAudio { get; set; }
    public string? ParentFolderHint { get; set; }

    public string BuildAiEvidencePrompt()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Raw Filename: {RawFileName}");
        sb.AppendLine($"Sanitized Title Candidate: {CleanTitleCandidate}");
        if (ExtractedYear.HasValue) sb.AppendLine($"Filename Year Candidate: {ExtractedYear.Value}");
        if (!string.IsNullOrWhiteSpace(ExtractedSeasonEpisode)) sb.AppendLine($"TV Show Season/Episode: {ExtractedSeasonEpisode}");
        if (!string.IsNullOrWhiteSpace(DetectedLanguage)) sb.AppendLine($"Detected Language Hint: {DetectedLanguage}");
        if (!string.IsNullOrWhiteSpace(EmbeddedTitle)) sb.AppendLine($"Embedded Tag Title: {EmbeddedTitle}");
        if (!string.IsNullOrWhiteSpace(EmbeddedArtist)) sb.AppendLine($"Embedded Tag Artist/Singer: {EmbeddedArtist}");
        if (!string.IsNullOrWhiteSpace(EmbeddedAlbum)) sb.AppendLine($"Embedded Tag Album: {EmbeddedAlbum}");
        if (!string.IsNullOrWhiteSpace(EmbeddedGenre)) sb.AppendLine($"Embedded Tag Genre: {EmbeddedGenre}");
        if (!string.IsNullOrWhiteSpace(EmbeddedDate)) sb.AppendLine($"Embedded Tag Date: {EmbeddedDate}");
        if (StreamLanguages.Count > 0) sb.AppendLine($"Media Stream Languages: {string.Join(", ", StreamLanguages)}");
        if (DurationSeconds > 0) sb.AppendLine($"Duration: {Math.Round(DurationSeconds / 60.0, 1)} minutes");
        if (Height > 0) sb.AppendLine($"Resolution: {Width}x{Height}");
        else sb.AppendLine("Media Stream Type: Audio-only");
        if (!string.IsNullOrWhiteSpace(ParentFolderHint)) sb.AppendLine($"Folder Context Hint: {ParentFolderHint}");

        return sb.ToString();
    }
}

public class MediaMetadataHarvester
{
    private readonly IMediaToolRunner _toolRunner;
    private readonly ILogger<MediaMetadataHarvester> _logger;

    private static readonly Dictionary<string, string> IsoLanguageMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["kan"] = "Kannada", ["knd"] = "Kannada",
        ["hin"] = "Hindi", ["hnd"] = "Hindi",
        ["eng"] = "English",
        ["tam"] = "Tamil",
        ["tel"] = "Telugu",
        ["mal"] = "Malayalam",
        ["ben"] = "Bengali",
        ["mar"] = "Marathi",
        ["guj"] = "Gujarati",
        ["pan"] = "Punjabi", ["pun"] = "Punjabi",
        ["urd"] = "Urdu",
        ["san"] = "Sanskrit",
        ["fre"] = "French", ["fra"] = "French",
        ["spa"] = "Spanish",
        ["ger"] = "German", ["deu"] = "German",
        ["ita"] = "Italian",
        ["jpn"] = "Japanese",
        ["kor"] = "Korean",
        ["chi"] = "Chinese", ["zho"] = "Chinese",
        ["rus"] = "Russian",
        ["ara"] = "Arabic"
    };

    private static readonly string[] KnownLanguages = 
    { 
        "Kannada", "Hindi", "English", "Tamil", "Telugu", 
        "Malayalam", "Bengali", "Marathi", "Gujarati", "Punjabi", 
        "Sanskrit", "Spanish", "French", "German", "Japanese", "Korean" 
    };

    public MediaMetadataHarvester(IMediaToolRunner toolRunner, ILogger<MediaMetadataHarvester> logger)
    {
        _toolRunner = toolRunner;
        _logger = logger;
    }

    public async Task<HarvestedMediaEvidence> HarvestAsync(Video video, CancellationToken cancellationToken = default)
    {
        var evidence = new HarvestedMediaEvidence
        {
            RawFileName = video.FileName,
            DurationSeconds = video.Duration,
            Height = video.Height,
            Width = video.Width,
            IsAudio = IsAudioFile(video.FileName, video.Height)
        };

        // Extract folder context hint (if file is inside a named subfolder)
        try
        {
            if (!string.IsNullOrWhiteSpace(video.FilePath))
            {
                var dir = Path.GetDirectoryName(video.FilePath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    var parentName = Path.GetFileName(dir);
                    if (!string.Equals(parentName, "media", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(parentName, "downloads", StringComparison.OrdinalIgnoreCase))
                    {
                        evidence.ParentFolderHint = parentName;
                    }
                }
            }
        }
        catch { }

        // 1. Parse scene release filename
        ParseFilename(video.FileName, evidence);

        // 2. Extract embedded tags via ffprobe if physical file exists
        if (!string.IsNullOrWhiteSpace(video.FilePath) && File.Exists(video.FilePath))
        {
            await ExtractEmbeddedTagsWithFfprobeAsync(video.FilePath, evidence, cancellationToken);
        }

        // 3. Fallback: check if Video entity already had stream audio info
        if (evidence.StreamLanguages.Count == 0 && !string.IsNullOrWhiteSpace(video.AudioTracksJson))
        {
            try
            {
                var tracks = JsonSerializer.Deserialize<List<AudioTrackInfo>>(video.AudioTracksJson);
                if (tracks != null)
                {
                    foreach (var t in tracks)
                    {
                        if (!string.IsNullOrWhiteSpace(t.Language))
                        {
                            var normalized = NormalizeLanguageCode(t.Language);
                            if (!evidence.StreamLanguages.Contains(normalized))
                            {
                                evidence.StreamLanguages.Add(normalized);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        // If detected language is not set from filename, use primary audio stream language
        if (string.IsNullOrWhiteSpace(evidence.DetectedLanguage) && evidence.StreamLanguages.Count > 0)
        {
            evidence.DetectedLanguage = evidence.StreamLanguages.First();
        }

        // If embedded artist or title is present and filename was cryptic, prefer embedded
        if (string.IsNullOrWhiteSpace(evidence.CleanTitleCandidate) && !string.IsNullOrWhiteSpace(evidence.EmbeddedTitle))
        {
            evidence.CleanTitleCandidate = evidence.EmbeddedTitle;
        }

        return evidence;
    }

    private static void ParseFilename(string fileName, HarvestedMediaEvidence evidence)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

        // Check for Language hint in filename
        foreach (var lang in KnownLanguages)
        {
            if (Regex.IsMatch(nameWithoutExt, $@"\b{lang}\b", RegexOptions.IgnoreCase))
            {
                evidence.DetectedLanguage = lang;
                break;
            }
        }

        // Check for TV Show pattern: S01E01 or 1x01
        var seMatch = Regex.Match(nameWithoutExt, @"\b[sS](\d{1,2})[eE](\d{1,2})\b");
        if (seMatch.Success)
        {
            var s = int.Parse(seMatch.Groups[1].Value);
            var e = int.Parse(seMatch.Groups[2].Value);
            evidence.ExtractedSeasonEpisode = $"S{s:D2}E{e:D2}";
        }
        else
        {
            var altMatch = Regex.Match(nameWithoutExt, @"\b(\d{1,2})x(\d{1,2})\b");
            if (altMatch.Success)
            {
                var s = int.Parse(altMatch.Groups[1].Value);
                var e = int.Parse(altMatch.Groups[2].Value);
                evidence.ExtractedSeasonEpisode = $"S{s:D2}E{e:D2}";
            }
        }

        // Check for Year pattern: (19\d{2}|20\d{2})
        var yearMatches = Regex.Matches(nameWithoutExt, @"\b(19\d{2}|20\d{2})\b");
        if (yearMatches.Count > 0)
        {
            // Pick the last valid release year (often at end of title before scene tags)
            if (int.TryParse(yearMatches[^1].Value, out var y) && y >= 1900 && y <= DateTime.UtcNow.Year + 2)
            {
                evidence.ExtractedYear = y;
            }
        }

        // Clean scene release tags
        var clean = nameWithoutExt;

        // Remove square/round bracket blocks that are purely tags or groups (e.g. [YTS.MX], [1080p], (2022))
        clean = Regex.Replace(clean, @"\[.*?\]", " ");
        
        // Remove common scene keywords
        var sceneRegex = @"\b(1080p|720p|2160p|4k|uhd|480p|576p|bluray|bdrip|brrip|web-dl|webrip|web|hdtv|dvdrip|hdrip|cam|telesync|remux|x264|x265|h264|h265|hevc|avc|10bit|8bit|aac|ac3|dts|ddp5\.1|dts-hd|truehd|atmos|mp3|flac|opus|rarbg|yts|fgt|sparks|ettv|tgx|psa|galaxy|qxr|dual\s*audio|hindi\s*dubbed|subbed|esub|repack|proper)\b";
        clean = Regex.Replace(clean, sceneRegex, " ", RegexOptions.IgnoreCase);

        // Remove year from title if already extracted
        if (evidence.ExtractedYear.HasValue)
        {
            clean = Regex.Replace(clean, $@"\b{evidence.ExtractedYear.Value}\b", " ");
        }

        // Remove season/episode string from title if extracted
        if (!string.IsNullOrWhiteSpace(evidence.ExtractedSeasonEpisode))
        {
            clean = Regex.Replace(clean, @"\b[sS]\d{1,2}[eE]\d{1,2}\b", " ");
            clean = Regex.Replace(clean, @"\b\d{1,2}x\d{1,2}\b", " ");
        }

        // Remove leading track numbers in audio (e.g. "01. ", "01 - ")
        clean = Regex.Replace(clean, @"^\s*\d{1,3}[\.\-_\s]+", "");

        // Replace dots, underscores, dashes with space
        clean = clean.Replace('.', ' ').Replace('_', ' ');

        // Collapse multiple spaces
        clean = Regex.Replace(clean, @"\s+", " ").Trim();

        evidence.CleanTitleCandidate = clean;
    }

    private async Task ExtractEmbeddedTagsWithFfprobeAsync(string filePath, HarvestedMediaEvidence evidence, CancellationToken ct)
    {
        try
        {
            var args = new[] { "-v", "error", "-show_format", "-show_streams", "-of", "json", filePath };
            var result = await _toolRunner.RunToolAsync("ffprobe", args, ct, timeout: TimeSpan.FromSeconds(20));

            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                return;
            }

            using var doc = JsonDocument.Parse(result.StandardOutput);
            var root = doc.RootElement;

            // 1. Format tags
            if (root.TryGetProperty("format", out var formatProp))
            {
                if (formatProp.TryGetProperty("duration", out var durProp) && 
                    double.TryParse(durProp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var dur))
                {
                    if (evidence.DurationSeconds <= 0) evidence.DurationSeconds = dur;
                }

                if (formatProp.TryGetProperty("tags", out var formatTags))
                {
                    evidence.EmbeddedTitle = GetTagCaseInsensitive(formatTags, "title");
                    evidence.EmbeddedArtist = GetTagCaseInsensitive(formatTags, "artist") ?? GetTagCaseInsensitive(formatTags, "album_artist");
                    evidence.EmbeddedAlbum = GetTagCaseInsensitive(formatTags, "album");
                    evidence.EmbeddedGenre = GetTagCaseInsensitive(formatTags, "genre");
                    evidence.EmbeddedDate = GetTagCaseInsensitive(formatTags, "date") ?? GetTagCaseInsensitive(formatTags, "year");

                    // Try parse year from embedded date (e.g. "2022-10-14")
                    if (!evidence.ExtractedYear.HasValue && !string.IsNullOrWhiteSpace(evidence.EmbeddedDate))
                    {
                        var m = Regex.Match(evidence.EmbeddedDate, @"\b(19\d{2}|20\d{2})\b");
                        if (m.Success && int.TryParse(m.Value, out var y))
                        {
                            evidence.ExtractedYear = y;
                        }
                    }
                }
            }

            // 2. Stream tags (audio and subtitle languages)
            if (root.TryGetProperty("streams", out var streams) && streams.ValueKind == JsonValueKind.Array)
            {
                foreach (var stream in streams.EnumerateArray())
                {
                    if (stream.TryGetProperty("codec_type", out var typeEl))
                    {
                        var codecType = typeEl.GetString()?.ToLowerInvariant();
                        if (codecType is "audio" or "subtitle")
                        {
                            if (stream.TryGetProperty("tags", out var sTags))
                            {
                                var langCode = GetTagCaseInsensitive(sTags, "language");
                                if (!string.IsNullOrWhiteSpace(langCode))
                                {
                                    var langName = NormalizeLanguageCode(langCode);
                                    if (!evidence.StreamLanguages.Contains(langName))
                                    {
                                        evidence.StreamLanguages.Add(langName);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed extracting embedded tags via ffprobe for {FilePath}", filePath);
        }
    }

    private static string? GetTagCaseInsensitive(JsonElement tags, string key)
    {
        foreach (var prop in tags.EnumerateObject())
        {
            if (string.Equals(prop.Name, key, StringComparison.OrdinalIgnoreCase))
            {
                var val = prop.Value.GetString()?.Trim();
                return string.IsNullOrWhiteSpace(val) ? null : val;
            }
        }
        return null;
    }

    public static string NormalizeLanguageCode(string rawCode)
    {
        var cleaned = rawCode.Trim().ToLowerInvariant();
        if (IsoLanguageMap.TryGetValue(cleaned, out var full))
        {
            return full;
        }

        // If it's already a recognized full name like "Kannada" or "English"
        var match = KnownLanguages.FirstOrDefault(l => string.Equals(l, cleaned, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;

        // Fallback: title-case the code
        return char.ToUpper(cleaned[0]) + (cleaned.Length > 1 ? cleaned[1..] : "");
    }

    public static bool IsAudioFile(string fileName, int height = 0)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".mp3" or ".m4a" or ".aac" or ".flac" or ".wav" or ".ogg" or ".opus" || (height == 0 && ext is ".mkv" or ".webm" or ".mp4" && ext.StartsWith("."));
    }
}
