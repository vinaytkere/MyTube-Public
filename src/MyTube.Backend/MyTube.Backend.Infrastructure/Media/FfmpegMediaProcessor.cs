using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application;

namespace MyTube.Backend.Infrastructure.Media;

public class FfmpegMediaProcessor : IMediaProcessor
{
    private readonly string _mediaRoot;
    private readonly IMediaToolRunner _toolRunner;
    private readonly IMediaProbeService _probeService;
    private readonly MediaOutputValidator _validator;
    private readonly IFfmpegRunner _ffmpegRunner;
    private readonly ILogger<FfmpegMediaProcessor> _logger;

    public FfmpegMediaProcessor(
        IConfiguration configuration,
        IMediaToolRunner toolRunner,
        IMediaProbeService probeService,
        MediaOutputValidator validator,
        IFfmpegRunner ffmpegRunner,
        ILogger<FfmpegMediaProcessor> logger)
    {
        _mediaRoot = configuration["Media:RootPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "media");
        _toolRunner = toolRunner;
        _probeService = probeService;
        _validator = validator;
        _ffmpegRunner = ffmpegRunner;
        _logger = logger;
    }

    public async Task<MediaProbeResult> ProbeAsync(string path, CancellationToken token = default)
    {
        return await _probeService.ProbeFileAsync(path, token);
    }

    public async Task<MediaProcessResult> ProcessMediaAsync(MediaProcessRequest request, CancellationToken token = default)
    {
        var processResult = new MediaProcessResult();
        _logger.LogInformation("FfmpegMediaProcessor: Starting media processing for job {JobId}. Target: {OutputPath}",
            request.JobId, request.OutputPath);

        var outputDir = Path.GetDirectoryName(request.OutputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        if (string.IsNullOrWhiteSpace(request.InputPath) || !File.Exists(request.InputPath))
        {
            var ex = new MediaProcessingException(
                $"Source file does not exist at '{request.InputPath}'.",
                "SOURCE_FILE_NOT_FOUND",
                "filesystem",
                -1,
                "Probing",
                request.JobId,
                $"Path: {request.InputPath}",
                "The source media file could not be found for processing.",
                isRetryable: false,
                inputPath: request.InputPath,
                outputPath: request.OutputPath);

            processResult.Exception = ex;
            return processResult;
        }

        // 1. Probe local source file with FFprobe
        var sourceProbe = await _probeService.ProbeFileAsync(request.InputPath, token);
        if (!sourceProbe.IsReadable || sourceProbe.Classification == MediaClassification.Invalid)
        {
            var ex = new MediaProcessingException(
                $"Source media file is unreadable or contains invalid streams. Error: {sourceProbe.ErrorMessage}",
                "SOURCE_MEDIA_INVALID",
                "ffprobe",
                sourceProbe.ExitCode,
                "Probing",
                request.JobId,
                sourceProbe.ErrorMessage ?? "Unreadable source",
                "The source media file cannot be decoded or is corrupted.",
                isRetryable: false,
                inputPath: request.InputPath,
                outputPath: request.OutputPath);

            processResult.Exception = ex;
            return processResult;
        }

        var expectedClassification = request.OutputMediaType == OutputMediaType.Audio
            ? MediaClassification.AudioOnly
            : (sourceProbe.HasAudio ? MediaClassification.VideoWithAudio : MediaClassification.VideoOnly);

        var sourceExt = Path.GetExtension(request.InputPath)?.TrimStart('.').ToLowerInvariant();
        var targetExt = Path.GetExtension(request.OutputPath)?.TrimStart('.').ToLowerInvariant();
        bool isSameFormat = string.Equals(sourceExt, targetExt, StringComparison.OrdinalIgnoreCase);

        var args = new List<string> { "-y", "-i", request.InputPath };

        var isStreamCopy = isSameFormat;

        if (request.OutputMediaType == OutputMediaType.Audio)
        {
            args.Add("-vn");
            if (isSameFormat)
            {
                args.AddRange(new[] { "-c:a", "copy" });
            }
            else
            {
                switch (request.OutputFormat)
                {
                    case OutputFormat.Mp3:
                        args.AddRange(new[] { "-c:a", "libmp3lame", "-b:a", request.AudioBitrate ?? "192k", "-threads", "0" });
                        break;
                    case OutputFormat.M4a:
                        args.AddRange(new[] { "-c:a", "aac", "-b:a", request.AudioBitrate ?? "192k", "-threads", "0" });
                        break;
                    case OutputFormat.Opus:
                        args.AddRange(new[] { "-c:a", "libopus", "-b:a", request.AudioBitrate ?? "128k", "-threads", "0" });
                        break;
                    case OutputFormat.Wav:
                        args.AddRange(new[] { "-c:a", "pcm_s16le", "-threads", "0" });
                        break;
                    default:
                        args.AddRange(new[] { "-c:a", "libmp3lame", "-b:a", "192k", "-threads", "0" });
                        break;
                }
            }
        }
        else
        {
            var subtitleCodec = string.Equals(targetExt, "mp4", StringComparison.OrdinalIgnoreCase) ? "mov_text" : "copy";

            if (isSameFormat)
            {
                args.AddRange(new[] { "-map", "0:v:0", "-map", "0:a?", "-map", "0:s?", "-c:v", "copy", "-c:a", "copy", "-c:s", subtitleCodec });
            }
            else
            {
                var isVideoMp4Compatible = !string.IsNullOrWhiteSpace(sourceProbe.VideoCodec) &&
                    (sourceProbe.VideoCodec.Contains("264", StringComparison.OrdinalIgnoreCase) ||
                     sourceProbe.VideoCodec.Contains("avc", StringComparison.OrdinalIgnoreCase) ||
                     sourceProbe.VideoCodec.Contains("265", StringComparison.OrdinalIgnoreCase) ||
                     sourceProbe.VideoCodec.Contains("hevc", StringComparison.OrdinalIgnoreCase) ||
                     sourceProbe.VideoCodec.Contains("vp9", StringComparison.OrdinalIgnoreCase) ||
                     sourceProbe.VideoCodec.Contains("vp09", StringComparison.OrdinalIgnoreCase) ||
                     sourceProbe.VideoCodec.Contains("av1", StringComparison.OrdinalIgnoreCase) ||
                     sourceProbe.VideoCodec.Contains("av01", StringComparison.OrdinalIgnoreCase));

                var isAudioAac = !string.IsNullOrWhiteSpace(sourceProbe.AudioCodec) &&
                    (sourceProbe.AudioCodec.Contains("aac", StringComparison.OrdinalIgnoreCase) ||
                     sourceProbe.AudioCodec.Contains("mp4a", StringComparison.OrdinalIgnoreCase));

                if (isVideoMp4Compatible && isAudioAac && string.Equals(targetExt, "mp4", StringComparison.OrdinalIgnoreCase))
                {
                    isStreamCopy = true;
                    args.AddRange(new[] { "-map", "0:v:0", "-map", "0:a?", "-map", "0:s?", "-c:v", "copy", "-c:a", "copy", "-c:s", "mov_text" });
                }
                else if (isVideoMp4Compatible && string.Equals(targetExt, "mp4", StringComparison.OrdinalIgnoreCase))
                {
                    // Video is stream-copied directly (H264/HEVC/VP9/AV1) with zero quality loss; only audio is converted to AAC (takes ~1s)
                    isStreamCopy = true;
                    args.AddRange(new[] { "-map", "0:v:0", "-map", "0:a?", "-map", "0:s?", "-c:v", "copy", "-c:a", "aac", "-b:a", "192k", "-c:s", "mov_text", "-threads", "0" });
                }
                else
                {
                    args.AddRange(new[] { "-map", "0:v:0", "-map", "0:a?", "-map", "0:s?", "-c:v", "libx264", "-preset", "veryfast", "-crf", "22", "-c:a", "aac", "-b:a", "192k", "-c:s", subtitleCodec, "-threads", "0" });
                }
            }
        }

        if (string.Equals(targetExt, "mp4", StringComparison.OrdinalIgnoreCase))
        {
            args.AddRange(new[] { "-movflags", "+faststart" });
        }

        args.Add(request.OutputPath);

        _logger.LogInformation("Job {JobId}: [BREAKPOINT:FfmpegMediaProcessor:START] Executing FFmpeg (isStreamCopy={StreamCopy}, VideoCodec={VideoCodec}, AudioCodec={AudioCodec})",
            request.JobId, isStreamCopy, sourceProbe.VideoCodec, sourceProbe.AudioCodec);

        Action<TimeSpan>? onProgressTimestamp = null;
        var totalDuration = request.ExpectedDuration > TimeSpan.Zero ? request.ExpectedDuration : sourceProbe.Duration;
        if (totalDuration > TimeSpan.Zero && request.OnProgressPercent != null)
        {
            onProgressTimestamp = ts =>
            {
                var p = ts.TotalSeconds / totalDuration.TotalSeconds;
                request.OnProgressPercent(Math.Clamp(p, 0.0, 1.0));
            };
        }

        var swFf = System.Diagnostics.Stopwatch.StartNew();
        var execRes = await _ffmpegRunner.RunFfmpegAsync(args, token, onProgressTimestamp: onProgressTimestamp);
        swFf.Stop();
        _logger.LogInformation("Job {JobId}: [BREAKPOINT:FfmpegMediaProcessor:FFmpegExit] FFmpeg finished in {ElapsedMs}ms with Code {Code}",
            request.JobId, swFf.ElapsedMilliseconds, execRes.ExitCode);

        if (execRes.ExitCode != 0 || !File.Exists(request.OutputPath))
        {
            var ex = new MediaProcessingException(
                $"FFmpeg processing failed with code {execRes.ExitCode}.",
                "PROCESSING_FAILED",
                "ffmpeg",
                execRes.ExitCode,
                "Processing",
                request.JobId,
                execRes.StandardError,
                "Failed processing media file.",
                isRetryable: false,
                commandArguments: args,
                inputPath: request.InputPath,
                outputPath: request.OutputPath);

            processResult.Exception = ex;
            processResult.ErrorMessage = execRes.StandardError;
            processResult.ExitCode = execRes.ExitCode;
            return processResult;
        }

        var swVal = System.Diagnostics.Stopwatch.StartNew();
        var expectedDuration = request.ExpectedDuration > TimeSpan.Zero ? request.ExpectedDuration : sourceProbe.Duration;
        var val = await _validator.ValidateOutputAsync(request.OutputPath, expectedClassification, expectedDuration, isStreamCopy: isStreamCopy, token);
        swVal.Stop();
        _logger.LogInformation("Job {JobId}: [BREAKPOINT:FfmpegMediaProcessor:Validation] Output validation completed in {ElapsedMs}ms (IsValid={IsValid})",
            request.JobId, swVal.ElapsedMilliseconds, val.IsValid);

        if (!val.IsValid)
        {
            var ex = new MediaProcessingException(
                $"Output validation failed: {val.ErrorMessage}",
                "VALIDATION_FAILED",
                "ffprobe",
                -1,
                "Validating",
                request.JobId,
                val.ErrorMessage ?? "Validation failed",
                val.ErrorMessage ?? "Final media file validation failed.",
                isRetryable: false,
                outputPath: request.OutputPath);

            processResult.Exception = ex;
            return processResult;
        }

        processResult.Success = true;
        processResult.OutputPath = request.OutputPath;
        return processResult;
    }

    public async Task<MediaValidationResult> ValidateOutputAsync(
        string path,
        OutputMediaType expectedMediaType,
        OutputFormat expectedFormat,
        TimeSpan expectedDuration,
        CancellationToken token = default)
    {
        var fileProbe = await _probeService.ProbeFileAsync(path, token);
        if (!fileProbe.IsReadable)
        {
            return new MediaValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Output container is not readable by ffprobe. Detail: {fileProbe.ErrorMessage}"
            };
        }

        MediaClassification expectedClassification;
        if (expectedMediaType == OutputMediaType.Audio)
        {
            expectedClassification = MediaClassification.AudioOnly;
        }
        else
        {
            // If the output file has an audio track, validate as VideoWithAudio.
            // If the source was silent (e.g. stock clips, screen captures, timelapses), validate as VideoOnly.
            expectedClassification = fileProbe.HasAudio
                ? MediaClassification.VideoWithAudio
                : MediaClassification.VideoOnly;
        }

        return await _validator.ValidateOutputAsync(path, expectedClassification, expectedDuration, isStreamCopy: true, token);
    }

    public string ResolvePhysicalFilePath(string filePath)
    {
        if (File.Exists(filePath)) return filePath;

        try
        {
            var fileName = Path.GetFileName(filePath);
            var searchDirs = new[]
            {
                _mediaRoot,
                "/media",
                "/app/media",
                Path.Combine(Directory.GetCurrentDirectory(), "media"),
                Directory.GetCurrentDirectory()
            }.Where(d => !string.IsNullOrWhiteSpace(d) && Directory.Exists(d)).Distinct();

            foreach (var dir in searchDirs)
            {
                var directPath = Path.Combine(dir, fileName);
                if (File.Exists(directPath)) return directPath;

                var candidates = Directory.GetFiles(dir, fileName, SearchOption.AllDirectories);
                if (candidates.Length > 0) return candidates[0];

                var targetStem = Path.GetFileNameWithoutExtension(fileName);
                var targetNormalized = System.Text.RegularExpressions.Regex.Replace(targetStem, @"\s+", " ").Trim().ToLower();

                var allFiles = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
                foreach (var candidate in allFiles)
                {
                    var candStem = Path.GetFileNameWithoutExtension(candidate);
                    var candNormalized = System.Text.RegularExpressions.Regex.Replace(candStem, @"\s+", " ").Trim().ToLower();

                    if (candNormalized == targetNormalized || candNormalized.Contains(targetNormalized) || targetNormalized.Contains(candNormalized))
                    {
                        return candidate;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ResolvePhysicalFilePath error for {FilePath}", filePath);
        }

        return filePath;
    }

    public async Task<VideoMetadata?> ExtractMetadataAsync(string filePath)
    {
        try
        {
            filePath = ResolvePhysicalFilePath(filePath);
            if (!File.Exists(filePath)) return null;

            var args = new[] { "-v", "error", "-show_streams", "-show_format", "-of", "json", filePath };
            var result = await _toolRunner.RunToolAsync("ffprobe", args, timeout: TimeSpan.FromSeconds(30));

            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                _logger.LogWarning("ffprobe returned error (code {ExitCode}): {Error}", result.ExitCode, result.StandardError);
                return null;
            }

            var probeResult = JsonSerializer.Deserialize<FFprobeResult>(result.StandardOutput, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (probeResult == null) return null;

            var metadata = new VideoMetadata();

            if (probeResult.Format != null && !string.IsNullOrEmpty(probeResult.Format.Duration))
            {
                if (double.TryParse(probeResult.Format.Duration, NumberStyles.Any, CultureInfo.InvariantCulture, out var dur))
                {
                    metadata.Duration = dur;
                }
            }

            var videoStream = probeResult.Streams?.Find(s => string.Equals(s.Codec_Type, "video", StringComparison.OrdinalIgnoreCase));
            if (videoStream != null)
            {
                metadata.Width = videoStream.Width;
                metadata.Height = videoStream.Height;
                metadata.VideoCodec = videoStream.Codec_Name ?? string.Empty;

                if (!string.IsNullOrEmpty(videoStream.R_Frame_Rate))
                {
                    var parts = videoStream.R_Frame_Rate.Split('/');
                    if (parts.Length == 2 &&
                        double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var num) &&
                        double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var den) &&
                        den > 0)
                    {
                        metadata.FrameRate = num / den;
                    }
                }
            }

            var audioStreams = probeResult.Streams?.Where(s => string.Equals(s.Codec_Type, "audio", StringComparison.OrdinalIgnoreCase)).ToList();
            if (audioStreams != null && audioStreams.Count > 0)
            {
                metadata.AudioCodec = audioStreams[0].Codec_Name ?? string.Empty;
                var audioTracks = new List<AudioTrackInfo>();
                for (int i = 0; i < audioStreams.Count; i++)
                {
                    var s = audioStreams[i];
                    var lang = s.Tags != null && s.Tags.TryGetValue("language", out var l) ? l : "";
                    var titleTag = s.Tags != null && s.Tags.TryGetValue("title", out var t) ? t : (s.Tags != null && s.Tags.TryGetValue("handler_name", out var h) ? h : "");
                    var displayTitle = !string.IsNullOrWhiteSpace(titleTag)
                        ? titleTag
                        : (!string.IsNullOrWhiteSpace(lang) ? lang.ToUpperInvariant() : $"Audio {i + 1}");

                    audioTracks.Add(new AudioTrackInfo
                    {
                        Index = i,
                        Language = lang,
                        Title = displayTitle,
                        Codec = s.Codec_Name ?? string.Empty
                    });
                }
                metadata.AudioTracks = audioTracks;
            }

            if (probeResult.Format?.Tags != null &&
                probeResult.Format.Tags.TryGetValue("title", out var title) &&
                !string.IsNullOrWhiteSpace(title))
            {
                metadata.DisplayTitle = title;
            }

            if (string.IsNullOrWhiteSpace(metadata.DisplayTitle))
            {
                metadata.DisplayTitle = FormatFileNameToTitle(Path.GetFileNameWithoutExtension(filePath));
            }

            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting metadata for {FilePath}", filePath);
            return null;
        }
    }

    public async Task<bool> GenerateThumbnailAsync(string filePath, string destinationThumbnailPath, double offsetSeconds = 5)
    {
        try
        {
            filePath = ResolvePhysicalFilePath(filePath);
            if (!File.Exists(filePath)) return false;

            var destDir = Path.GetDirectoryName(destinationThumbnailPath);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            if (offsetSeconds < 0) offsetSeconds = 5;

            var args = new[]
            {
                "-y",
                "-ss", offsetSeconds.ToString("0.0", CultureInfo.InvariantCulture),
                "-i", filePath,
                "-vframes", "1",
                "-q:v", "2",
                destinationThumbnailPath
            };

            var result = await _toolRunner.RunToolAsync("ffmpeg", args, timeout: TimeSpan.FromSeconds(30));
            return result.ExitCode == 0 && File.Exists(destinationThumbnailPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating thumbnail for {FilePath}", filePath);
            return false;
        }
    }

    public async Task<List<string>> ExtractAudioChunksAsync(string filePath, string tempDir)
    {
        var resultChunks = new List<string>();
        var resolvedPath = ResolvePhysicalFilePath(filePath);
        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException($"Media file not found at: {filePath}");
        }

        Directory.CreateDirectory(tempDir);
        var baseAudioPath = Path.Combine(tempDir, $"{Guid.NewGuid()}_lightweight.mp3");

        var args = new[]
        {
            "-y",
            "-loglevel", "error",
            "-ss", "0",
            "-t", "1800",
            "-i", resolvedPath,
            "-vn", "-sn", "-dn",
            "-ac", "1",
            "-ar", "16000",
            "-ab", "32k",
            baseAudioPath
        };

        var result = await _toolRunner.RunToolAsync("ffmpeg", args, timeout: TimeSpan.FromMinutes(3));
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"FFmpeg failed (code {result.ExitCode}): {result.StandardError}");
        }

        if (!File.Exists(baseAudioPath))
        {
            throw new InvalidOperationException($"FFmpeg completed with exit code 0 but output MP3 was not created at {baseAudioPath}");
        }

        var fileInfo = new FileInfo(baseAudioPath);
        if (fileInfo.Length <= 24 * 1024 * 1024)
        {
            resultChunks.Add(baseAudioPath);
            return resultChunks;
        }

        var chunkPattern = Path.Combine(tempDir, $"{Guid.NewGuid()}_chunk_%03d.mp3");
        var segArgs = new[]
        {
            "-y",
            "-i", baseAudioPath,
            "-f", "segment",
            "-segment_time", "600",
            "-c", "copy",
            chunkPattern
        };

        await _toolRunner.RunToolAsync("ffmpeg", segArgs, timeout: TimeSpan.FromMinutes(3));
        try { File.Delete(baseAudioPath); } catch { }

        var createdChunks = Directory.GetFiles(tempDir, "*_chunk_*.mp3");
        Array.Sort(createdChunks);
        resultChunks.AddRange(createdChunks);
        return resultChunks;
    }

    private static string FormatFileNameToTitle(string fileName)
    {
        var title = fileName.Replace('_', ' ').Replace('-', ' ');
        var words = title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpper(words[i][0]) + words[i][1..].ToLower();
            }
        }
        return string.Join(" ", words);
    }

    private class FFprobeResult
    {
        public List<FFprobeStream>? Streams { get; set; }
        public FFprobeFormat? Format { get; set; }
    }

    private class FFprobeStream
    {
        public string? Codec_Type { get; set; }
        public string? Codec_Name { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string? R_Frame_Rate { get; set; }
        public Dictionary<string, string>? Tags { get; set; }
    }

    private class FFprobeFormat
    {
        public string? Duration { get; set; }
        public string? Format_Name { get; set; }
        public Dictionary<string, string>? Tags { get; set; }
    }
}
