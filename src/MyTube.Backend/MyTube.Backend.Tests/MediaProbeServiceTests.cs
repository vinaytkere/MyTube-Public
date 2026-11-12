using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using MyTube.Backend.Application;
using MyTube.Backend.Infrastructure;
using Xunit;

namespace MyTube.Backend.Tests;

public class MediaProbeServiceTests : IDisposable
{
    private readonly string _tempFile;

    public MediaProbeServiceTests()
    {
        _tempFile = Path.GetTempFileName();
        File.WriteAllText(_tempFile, "dummy media content");
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempFile))
            {
                File.Delete(_tempFile);
            }
        }
        catch { }
    }

    private class FakeToolRunner : IMediaToolRunner
    {
        public Func<string, IReadOnlyList<string>, ToolExecutionResult> Handler { get; set; } = (_, _) =>
            new ToolExecutionResult(0, "{}", "");

        public string GetYtDlpPath() => "yt-dlp";
        public string GetFfmpegPath() => "ffmpeg";
        public string GetFfprobePath() => "ffprobe";

        public Task<ToolExecutionResult> RunToolAsync(
            string toolName,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken = default,
            TimeSpan? timeout = null,
            Action<string>? onStandardOutputLine = null,
            Action<string>? onStandardErrorLine = null,
            string? workingDirectory = null)
        {
            return Task.FromResult(Handler(toolName, arguments));
        }
    }

    [Fact]
    public async Task ProbeFileAsync_ValidVideoAndAudio_ParsesStreamsAndFormatCorrectly()
    {
        var fakeRunner = new FakeToolRunner();
        fakeRunner.Handler = (tool, args) =>
        {
            Assert.Equal("ffprobe", tool);
            Assert.Contains("-show_streams", args);
            Assert.Contains("-show_format", args);
            Assert.Contains("-of", args);
            Assert.Contains("json", args);

            var json = """
            {
                "streams": [
                    {
                        "codec_name": "h264",
                        "codec_type": "video",
                        "width": 1920,
                        "height": 1080,
                        "r_frame_rate": "30000/1001"
                    },
                    {
                        "codec_name": "aac",
                        "codec_type": "audio",
                        "sample_rate": "48000",
                        "channels": 2,
                        "channel_layout": "stereo"
                    }
                ],
                "format": {
                    "format_name": "mov,mp4,m4a,3gp,3g2,mj2",
                    "duration": "120.500000"
                }
            }
            """;
            return new ToolExecutionResult(0, json, "");
        };

        var service = new MediaProbeService(fakeRunner, NullLogger<MediaProbeService>.Instance);
        var result = await service.ProbeFileAsync(_tempFile);

        Assert.True(result.IsReadable);
        Assert.True(result.HasVideo);
        Assert.True(result.HasAudio);
        Assert.Equal("h264", result.VideoCodec);
        Assert.Equal(1920, result.Width);
        Assert.Equal(1080, result.Height);
        Assert.Equal("aac", result.AudioCodec);
        Assert.Equal(48000, result.SampleRate);
        Assert.Equal(2, result.ChannelCount);
        Assert.Equal("stereo", result.ChannelLayout);
        Assert.Equal("mov,mp4,m4a,3gp,3g2,mj2", result.ContainerFormat);
        Assert.Equal(TimeSpan.FromSeconds(120.5), result.Duration);
        Assert.True(result.FrameRate > 29.9 && result.FrameRate < 30.0);
    }

    [Fact]
    public async Task ProbeFileAsync_FallbackDurationFromStream_WhenFormatDurationMissing()
    {
        var fakeRunner = new FakeToolRunner();
        fakeRunner.Handler = (tool, args) =>
        {
            var json = """
            {
                "streams": [
                    {
                        "codec_name": "h264",
                        "codec_type": "video",
                        "width": 1280,
                        "height": 720,
                        "duration": "45.250000"
                    }
                ],
                "format": {
                    "format_name": "matroska,webm"
                }
            }
            """;
            return new ToolExecutionResult(0, json, "");
        };

        var service = new MediaProbeService(fakeRunner, NullLogger<MediaProbeService>.Instance);
        var result = await service.ProbeFileAsync(_tempFile);

        Assert.True(result.IsReadable);
        Assert.True(result.HasVideo);
        Assert.False(result.HasAudio);
        Assert.Equal(TimeSpan.FromSeconds(45.25), result.Duration);
    }

    [Fact]
    public async Task ProbeFileAsync_ToolExitsWithError_PopulatesExitCodeAndErrorMessage()
    {
        var fakeRunner = new FakeToolRunner();
        fakeRunner.Handler = (tool, args) =>
            new ToolExecutionResult(1, "", "Invalid data found when processing input");

        var service = new MediaProbeService(fakeRunner, NullLogger<MediaProbeService>.Instance);
        var result = await service.ProbeFileAsync(_tempFile);

        Assert.False(result.IsReadable);
        Assert.False(result.HasVideo);
        Assert.False(result.HasAudio);
        Assert.Equal(1, result.ExitCode);
        Assert.Equal("Invalid data found when processing input", result.ErrorMessage);
    }

    [Fact]
    public async Task ProbeFileAsync_NonExistentFile_ReturnsUnreadableResult()
    {
        var fakeRunner = new FakeToolRunner();
        var service = new MediaProbeService(fakeRunner, NullLogger<MediaProbeService>.Instance);

        var result = await service.ProbeFileAsync(Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}.mp4"));

        Assert.False(result.IsReadable);
        Assert.False(result.HasVideo);
        Assert.False(result.HasAudio);
    }

    [Fact]
    public async Task ProbeUrlAsync_WithSubtitlesAndAutoCaptions_DetectsSubtitlesAvailable()
    {
        var fakeRunner = new FakeToolRunner();
        fakeRunner.Handler = (tool, args) =>
        {
            Assert.Equal("yt-dlp", tool);
            var json = """
            {
                "title": "Tutorial Video with Captions",
                "duration": 300,
                "subtitles": {
                    "en": [{ "ext": "vtt" }],
                    "es": [{ "ext": "vtt" }]
                },
                "automatic_captions": {
                    "fr": [{ "ext": "vtt" }]
                },
                "formats": [
                    { "height": 1080 }
                ]
            }
            """;
            return new ToolExecutionResult(0, json, "");
        };

        var service = new MediaProbeService(fakeRunner, NullLogger<MediaProbeService>.Instance);
        var result = await service.ProbeUrlAsync("https://www.youtube.com/watch?v=dummy123");

        Assert.Equal("Tutorial Video with Captions", result.Title);
        Assert.True(result.SubtitlesAvailable);
        Assert.Contains("en", result.SubtitleLanguages);
        Assert.Contains("es", result.SubtitleLanguages);
        // fr was an auto-caption while creator subtitles exist, so it is excluded to avoid 429
        Assert.DoesNotContain("fr", result.SubtitleLanguages);
    }

    [Fact]
    public async Task ProbeUrlAsync_NoSubtitles_DetectsSubtitlesUnavailable()
    {
        var fakeRunner = new FakeToolRunner();
        fakeRunner.Handler = (tool, args) =>
        {
            Assert.Equal("yt-dlp", tool);
            var json = """
            {
                "title": "Raw Camera Footage Without Captions",
                "duration": 60,
                "subtitles": {},
                "automatic_captions": {},
                "formats": [
                    { "height": 720 }
                ]
            }
            """;
            return new ToolExecutionResult(0, json, "");
        };

        var service = new MediaProbeService(fakeRunner, NullLogger<MediaProbeService>.Instance);
        var result = await service.ProbeUrlAsync("https://www.facebook.com/watch?v=dummy456");

        Assert.Equal("Raw Camera Footage Without Captions", result.Title);
        Assert.False(result.SubtitlesAvailable);
        Assert.Empty(result.SubtitleLanguages);
    }

    [Fact]
    public async Task SourceMetadataService_ProbeRemoteUrlAsync_DetectsSubtitlesAvailable()
    {
        var fakeRunner = new FakeToolRunner();
        fakeRunner.Handler = (tool, args) =>
        {
            Assert.Equal("yt-dlp", tool);
            var json = """
            {
                "title": "Tutorial Video with Captions",
                "duration": 120,
                "subtitles": {
                    "en": [{ "ext": "vtt" }],
                    "es": [{ "ext": "vtt" }]
                },
                "automatic_captions": {
                    "fr": [{ "ext": "vtt" }]
                },
                "formats": [
                    { "height": 1080 }
                ]
            }
            """;
            return new ToolExecutionResult(0, json, "");
        };

        var service = new MyTube.Backend.Infrastructure.Media.SourceMetadataService(fakeRunner, NullLogger<MyTube.Backend.Infrastructure.Media.SourceMetadataService>.Instance);
        var result = await service.ProbeRemoteUrlAsync("https://www.youtube.com/watch?v=dummy123");

        Assert.Equal("Tutorial Video with Captions", result.Title);
        Assert.True(result.SubtitlesAvailable);
        Assert.Contains("en", result.SubtitleLanguages);
        Assert.Contains("es", result.SubtitleLanguages);
        // fr was an auto-caption while creator subtitles exist, so it is excluded to avoid 429
        Assert.DoesNotContain("fr", result.SubtitleLanguages);
    }

    [Fact]
    public async Task ProbeUrlAsync_FiltersOutSyntheticAutoTranslationPairs()
    {
        var fakeRunner = new FakeToolRunner();
        fakeRunner.Handler = (tool, args) =>
        {
            Assert.Equal("yt-dlp", tool);
            var json = """
            {
                "title": "YouTube Video With Hundreds of Auto Translations",
                "duration": 300,
                "subtitles": {
                    "en": [{ "ext": "vtt" }]
                },
                "automatic_captions": {
                    "en-orig": [{ "ext": "vtt" }],
                    "en-US": [{ "ext": "vtt" }],
                    "en-es": [{ "ext": "vtt" }],
                    "en-fr": [{ "ext": "vtt" }],
                    "en-de": [{ "ext": "vtt" }],
                    "ne-es": [{ "ext": "vtt" }]
                },
                "formats": [
                    { "height": 1080 }
                ]
            }
            """;
            return new ToolExecutionResult(0, json, "");
        };

        var service = new MediaProbeService(fakeRunner, NullLogger<MediaProbeService>.Instance);
        var result = await service.ProbeUrlAsync("https://www.youtube.com/watch?v=dummy123");

        Assert.True(result.SubtitlesAvailable);
        Assert.Contains("en", result.SubtitleLanguages);
        // When creator subtitles exist, auto-captions and translation pairs should be filtered out to prevent 429 errors
        Assert.DoesNotContain("en-US", result.SubtitleLanguages);
        Assert.DoesNotContain("en-orig", result.SubtitleLanguages);
        Assert.DoesNotContain("en-es", result.SubtitleLanguages);
        Assert.DoesNotContain("en-fr", result.SubtitleLanguages);
        Assert.DoesNotContain("en-de", result.SubtitleLanguages);
        Assert.DoesNotContain("ne-es", result.SubtitleLanguages);
    }

    [Fact]
    public async Task ProbeUrlAsync_NoCreatorSubtitles_FallbacksToOriginalSpokenCaptionOnly()
    {
        var fakeRunner = new FakeToolRunner();
        fakeRunner.Handler = (tool, args) =>
        {
            Assert.Equal("yt-dlp", tool);
            var json = """
            {
                "title": "YouTube Video Without Creator Subtitles",
                "duration": 300,
                "subtitles": {},
                "automatic_captions": {
                    "en-orig": [{ "ext": "vtt" }],
                    "kn": [{ "ext": "vtt" }],
                    "es": [{ "ext": "vtt" }]
                },
                "formats": [
                    { "height": 1080 }
                ]
            }
            """;
            return new ToolExecutionResult(0, json, "");
        };

        var service = new MediaProbeService(fakeRunner, NullLogger<MediaProbeService>.Instance);
        var result = await service.ProbeUrlAsync("https://www.youtube.com/watch?v=dummy123");

        Assert.True(result.SubtitlesAvailable);
        Assert.Contains("en", result.SubtitleLanguages);
        // Auto-translated tracks like Kannada and Spanish should be strictly excluded
        Assert.DoesNotContain("kn", result.SubtitleLanguages);
        Assert.DoesNotContain("es", result.SubtitleLanguages);
    }

    [Fact]
    public async Task ProbeUrlAsync_WhenStdoutContainsLeadingWarnings_ParsesJsonPayloadSuccessfully()
    {
        var fakeRunner = new FakeToolRunner();
        fakeRunner.Handler = (tool, args) =>
        {
            Assert.Equal("yt-dlp", tool);
            var outputWithWarnings = """
            WARNING: [generic] Falling back on generic information extractor
            WARNING: [generic] unable to extract something minor
            {
                "title": "Siamese cat inside a hat",
                "duration": 19,
                "formats": [
                    { "height": 720, "audio_ext": "none" }
                ]
            }
            """;
            return new ToolExecutionResult(0, outputWithWarnings, "");
        };

        var service = new MediaProbeService(fakeRunner, NullLogger<MediaProbeService>.Instance);
        var result = await service.ProbeUrlAsync("https://mixkit.co/free-stock-video/siamese-cat-inside-a-hat-4103/");

        Assert.Equal("Siamese cat inside a hat", result.Title);
        Assert.Equal(19, result.DurationSeconds);
        Assert.Contains("720p (HD)", result.Qualities);
    }
}

