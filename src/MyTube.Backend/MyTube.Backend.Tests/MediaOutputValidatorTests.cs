using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using MyTube.Backend.Application;
using MyTube.Backend.Infrastructure.Media;
using Xunit;

namespace MyTube.Backend.Tests;

public class MediaOutputValidatorTests : IDisposable
{
    private readonly string _tempFile;

    public MediaOutputValidatorTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"val_test_{Guid.NewGuid():N}.mp4");
        File.WriteAllBytes(_tempFile, new byte[8192]); // valid size
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            try { File.Delete(_tempFile); } catch { }
        }
    }

    [Fact]
    public async Task ValidateOutputAsync_MissingFile_ReturnsInvalid()
    {
        var mockRunner = new FakeRunner();
        var mockProbe = new FakeProbe();
        var validator = new MediaOutputValidator(mockProbe, mockRunner, NullLogger<MediaOutputValidator>.Instance);

        var result = await validator.ValidateOutputAsync(
            "non_existent_file_12345.mp4",
            MediaClassification.VideoWithAudio,
            TimeSpan.FromSeconds(30));

        Assert.False(result.IsValid);
        Assert.Contains("does not exist", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateOutputAsync_SubMinimumFileSize_ReturnsInvalid()
    {
        var smallFile = Path.Combine(Path.GetTempPath(), $"small_{Guid.NewGuid():N}.mp4");
        await File.WriteAllBytesAsync(smallFile, new byte[100]); // below 4096 bytes

        try
        {
            var mockRunner = new FakeRunner();
            var mockProbe = new FakeProbe();
            var validator = new MediaOutputValidator(mockProbe, mockRunner, NullLogger<MediaOutputValidator>.Instance);

            var result = await validator.ValidateOutputAsync(
                smallFile,
                MediaClassification.VideoWithAudio,
                TimeSpan.FromSeconds(30));

            Assert.False(result.IsValid);
            Assert.Contains("below minimum threshold", result.ErrorMessage);
        }
        finally
        {
            if (File.Exists(smallFile)) File.Delete(smallFile);
        }
    }

    [Fact]
    public async Task ValidateOutputAsync_MissingVideoStream_ReturnsInvalid()
    {
        var mockRunner = new FakeRunner();
        var mockProbe = new FakeProbe
        {
            Result = new MediaProbeResult
            {
                IsReadable = true,
                HasVideo = false, // missing video
                HasAudio = true,
                Duration = TimeSpan.FromSeconds(30)
            }
        };

        var validator = new MediaOutputValidator(mockProbe, mockRunner, NullLogger<MediaOutputValidator>.Instance);

        var result = await validator.ValidateOutputAsync(
            _tempFile,
            MediaClassification.VideoWithAudio,
            TimeSpan.FromSeconds(30));

        Assert.False(result.IsValid);
        Assert.Contains("missing a video stream", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateOutputAsync_AudioOnlyWithUnexpectedVideo_ReturnsInvalid()
    {
        var mockRunner = new FakeRunner();
        var mockProbe = new FakeProbe
        {
            Result = new MediaProbeResult
            {
                IsReadable = true,
                HasVideo = true, // unexpected
                HasAudio = true,
                Duration = TimeSpan.FromSeconds(30)
            }
        };

        var validator = new MediaOutputValidator(mockProbe, mockRunner, NullLogger<MediaOutputValidator>.Instance);

        var result = await validator.ValidateOutputAsync(
            _tempFile,
            MediaClassification.AudioOnly,
            TimeSpan.FromSeconds(30));

        Assert.False(result.IsValid);
        Assert.Contains("unexpectedly contains a video stream", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateOutputAsync_DurationDriftExceedingTolerance_ReturnsInvalid()
    {
        var mockRunner = new FakeRunner();
        var mockProbe = new FakeProbe
        {
            Result = new MediaProbeResult
            {
                IsReadable = true,
                HasVideo = true,
                HasAudio = true,
                Duration = TimeSpan.FromSeconds(45) // expected 30, drift 15s
            }
        };

        var validator = new MediaOutputValidator(mockProbe, mockRunner, NullLogger<MediaOutputValidator>.Instance);

        var result = await validator.ValidateOutputAsync(
            _tempFile,
            MediaClassification.VideoWithAudio,
            TimeSpan.FromSeconds(30));

        Assert.False(result.IsValid);
        Assert.Contains("exceeding allowed tolerance", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateOutputAsync_ValidStreamsAndTolerance_ReturnsValid()
    {
        var mockRunner = new FakeRunner(); // decodability exitCode 0
        var mockProbe = new FakeProbe
        {
            Result = new MediaProbeResult
            {
                IsReadable = true,
                HasVideo = true,
                HasAudio = true,
                Duration = TimeSpan.FromSeconds(30.05) // within tolerance
            }
        };

        var validator = new MediaOutputValidator(mockProbe, mockRunner, NullLogger<MediaOutputValidator>.Instance);

        var result = await validator.ValidateOutputAsync(
            _tempFile,
            MediaClassification.VideoWithAudio,
            TimeSpan.FromSeconds(30));

        Assert.True(result.IsValid);
        Assert.True(result.IsDecodable);
        Assert.True(result.HasExpectedStreams);
    }

    [Fact]
    public async Task ValidateOutputAsync_SilentVideo_WhenVideoOnly_ReturnsValid()
    {
        var mockRunner = new FakeRunner();
        var mockProbe = new FakeProbe
        {
            Result = new MediaProbeResult
            {
                IsReadable = true,
                HasVideo = true,
                HasAudio = false, // silent video (e.g. stock clips, timelapses)
                Duration = TimeSpan.FromSeconds(19)
            }
        };

        var validator = new MediaOutputValidator(mockProbe, mockRunner, NullLogger<MediaOutputValidator>.Instance);

        var result = await validator.ValidateOutputAsync(
            _tempFile,
            MediaClassification.VideoOnly,
            TimeSpan.FromSeconds(19),
            isStreamCopy: true);

        Assert.True(result.IsValid);
        Assert.True(result.IsDecodable);
        Assert.True(result.HasExpectedStreams);
    }

    private class FakeRunner : IMediaToolRunner
    {
        public string GetYtDlpPath() => "yt-dlp";
        public string GetFfmpegPath() => "ffmpeg";
        public string GetFfprobePath() => "ffprobe";

        public int ExitCodeToReturn { get; set; } = 0;
        public string StderrToReturn { get; set; } = "";

        public Task<ToolExecutionResult> RunToolAsync(string toolName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default, TimeSpan? timeout = null, Action<string>? onStandardOutputLine = null, Action<string>? onStandardErrorLine = null, string? workingDirectory = null)
        {
            return Task.FromResult(new ToolExecutionResult(ExitCodeToReturn, "", StderrToReturn));
        }
    }

    private class FakeProbe : IMediaProbeService
    {
        public MediaProbeResult Result { get; set; } = new() { IsReadable = true, HasVideo = true, HasAudio = true, Duration = TimeSpan.FromSeconds(30) };

        public Task<MediaProbeResult> ProbeFileAsync(string path, CancellationToken token = default) =>
            Task.FromResult(Result);

        public Task<VideoProbeResultDto> ProbeUrlAsync(string url, CancellationToken token = default) =>
            Task.FromResult(new VideoProbeResultDto());

        public Task<PlaylistProbeResultDto> ProbePlaylistAsync(string url, CancellationToken token = default) =>
            Task.FromResult(new PlaylistProbeResultDto());
    }
}
