using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application;

namespace MyTube.Backend.Infrastructure.Media;

public class MediaOutputValidator
{
    private readonly IMediaProbeService _probeService;
    private readonly IMediaToolRunner _toolRunner;
    private readonly ILogger<MediaOutputValidator> _logger;

    public MediaOutputValidator(
        IMediaProbeService probeService,
        IMediaToolRunner toolRunner,
        ILogger<MediaOutputValidator> logger)
    {
        _probeService = probeService;
        _toolRunner = toolRunner;
        _logger = logger;
    }

    public async Task<MediaValidationResult> ValidateOutputAsync(
        string path,
        MediaClassification expectedClassification,
        TimeSpan expectedDuration,
        bool isStreamCopy = false,
        CancellationToken token = default)
    {
        var result = new MediaValidationResult { ExpectedDuration = expectedDuration };

        // 1. Structural Check: File existence and size
        if (!File.Exists(path))
        {
            result.ErrorMessage = $"Output media file does not exist at '{path}'.";
            return result;
        }

        var fileInfo = new FileInfo(path);
        var minSize = expectedClassification == MediaClassification.AudioOnly ? 1024 : 4096;
        if (fileInfo.Length < minSize)
        {
            result.ErrorMessage = $"Output file size ({fileInfo.Length} bytes) is below minimum threshold ({minSize} bytes).";
            return result;
        }

        // 2. FFprobe Stream Inspection
        var probe = await _probeService.ProbeFileAsync(path, token);
        if (!probe.IsReadable)
        {
            result.ErrorMessage = $"Output container is not readable by ffprobe. Detail: {probe.ErrorMessage}";
            return result;
        }

        switch (expectedClassification)
        {
            case MediaClassification.VideoWithAudio:
                if (!probe.HasVideo)
                {
                    result.ErrorMessage = "Validated video-with-audio output is missing a video stream.";
                    return result;
                }
                if (!probe.HasAudio)
                {
                    result.ErrorMessage = "Validated video-with-audio output is missing an audio stream.";
                    return result;
                }
                break;

            case MediaClassification.AudioOnly:
                if (!probe.HasAudio)
                {
                    result.ErrorMessage = "Validated audio-only output is missing an audio stream.";
                    return result;
                }
                if (probe.HasVideo)
                {
                    result.ErrorMessage = "Validated audio-only output unexpectedly contains a video stream.";
                    return result;
                }
                break;

            case MediaClassification.VideoOnly:
                if (!probe.HasVideo)
                {
                    result.ErrorMessage = "Validated video-only output is missing a video stream.";
                    return result;
                }
                if (probe.HasAudio)
                {
                    result.ErrorMessage = "Validated video-only output unexpectedly contains an audio stream.";
                    return result;
                }
                break;

            case MediaClassification.Invalid:
                result.ErrorMessage = "Cannot validate output with Invalid expected classification.";
                return result;
        }

        result.HasExpectedStreams = true;
        result.ActualDuration = probe.Duration;
        result.DurationDifferenceSeconds = Math.Abs(probe.Duration.TotalSeconds - expectedDuration.TotalSeconds);

        // 3. Media-type & strategy specific duration tolerance
        if (expectedDuration > TimeSpan.Zero)
        {
            double maxAllowedDrift;
            if (isStreamCopy)
            {
                // Stream copy is keyframe-bound and web sources frequently round duration to nearest whole second; allow up to 3.0s or 5%
                maxAllowedDrift = Math.Max(3.0, expectedDuration.TotalSeconds * 0.05);
            }
            else if (expectedClassification == MediaClassification.AudioOnly)
            {
                // MP3 padding / AAC priming frames: allow up to 3.0s or 3%
                maxAllowedDrift = Math.Max(3.0, expectedDuration.TotalSeconds * 0.03);
            }
            else
            {
                // Accurate video re-encode: 250ms or 2.5%
                maxAllowedDrift = Math.Max(0.25, expectedDuration.TotalSeconds * 0.025);
            }

            if (result.DurationDifferenceSeconds > maxAllowedDrift)
            {
                result.ErrorMessage = $"Output duration ({probe.Duration:hh\\:mm\\:ss\\.fff}) differs from expected ({expectedDuration:hh\\:mm\\:ss\\.fff}) by {result.DurationDifferenceSeconds:F2}s, exceeding allowed tolerance of {maxAllowedDrift:F2}s.";
                return result;
            }
        }

        // 4. Decodability Validation (Smoke test first 5 seconds with FFmpeg null output)
        var decodeArgs = new[] { "-v", "error", "-i", path, "-t", "5", "-f", "null", "-" };
        var decodeRes = await _toolRunner.RunToolAsync("ffmpeg", decodeArgs, token, timeout: TimeSpan.FromSeconds(30));
        if (decodeRes.ExitCode != 0)
        {
            result.ErrorMessage = $"Output failed decodability validation (exit code {decodeRes.ExitCode}): {decodeRes.StandardError}";
            return result;
        }

        result.IsDecodable = true;
        result.IsValid = true;
        return result;
    }
}
