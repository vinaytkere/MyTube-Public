using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MyTube.Backend.Application;
using MyTube.Backend.Application.Features.Downloads.Commands;
using MyTube.Backend.Application.Features.Downloads.Queries;
using MyTube.Backend.Application.Media;
using MyTube.Backend.Domain;
using MyTube.Backend.Infrastructure;
using Xunit;

namespace MyTube.Backend.Tests;

public class BatchDownloadTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "YouTube")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ", "YouTube")]
    [InlineData("https://www.facebook.com/watch?v=123456789", "Facebook")]
    [InlineData("https://fb.watch/xyz123/", "Facebook")]
    [InlineData("https://www.instagram.com/reel/C-abc123xyz/", "Instagram")]
    [InlineData("https://www.instagram.com/p/C-abc123xyz/", "Instagram")]
    [InlineData("https://www.tiktok.com/@user/video/1234567890", "TikTok")]
    [InlineData("https://x.com/user/status/1234567890", "Twitter")]
    [InlineData("https://twitter.com/user/status/1234567890", "Twitter")]
    [InlineData("https://vimeo.com/12345678", "Vimeo")]
    [InlineData("https://soundcloud.com/artist/track", "SoundCloud")]
    [InlineData("https://dailymotion.com/video/x12345", "Generic")]
    public void MediaPortalDetector_ShouldCorrectlyDetectPortal(string url, string expectedPortal)
    {
        var detected = MediaPortalDetector.DetectPortal(url);
        detected.Should().Be(expectedPortal);
    }

    [Theory]
    [InlineData("https://www.youtube.com/playlist?list=PL1234567890", true)]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PL1234567890", true)]
    [InlineData("https://soundcloud.com/artist/sets/my-album", true)]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", false)]
    [InlineData("https://www.facebook.com/watch?v=123", false)]
    public void MediaPortalDetector_ShouldDetectPlaylists(string url, bool expectedIsPlaylist)
    {
        var isPlaylist = MediaPortalDetector.IsPlaylistUrl(url);
        isPlaylist.Should().Be(expectedIsPlaylist);
    }

    [Fact]
    public void MediaPortalDetector_ShouldNormalizeUrlAndStripTrackingParameters()
    {
        var dirtyUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ&si=123abc&fbclid=xyz987&utm_source=twitter";
        var cleaned = MediaPortalDetector.NormalizeUrl(dirtyUrl);

        cleaned.Should().Contain("v=dQw4w9WgXcQ");
        cleaned.Should().NotContain("si=");
        cleaned.Should().NotContain("fbclid=");
        cleaned.Should().NotContain("utm_source=");
    }

    [Fact]
    public void StartBatchDownloadCommandValidator_ShouldRejectNullOrEmptyRequests()
    {
        var validator = new StartBatchDownloadCommandValidator();

        var nullReq = validator.Validate(new StartBatchDownloadCommand(null!));
        nullReq.IsValid.Should().BeFalse();

        var emptyList = validator.Validate(new StartBatchDownloadCommand(new BatchDownloadRequest { Urls = new List<string>() }));
        emptyList.IsValid.Should().BeFalse();
        emptyList.Errors.Should().Contain(e => e.ErrorMessage.Contains("At least one URL"));
    }

    [Fact]
    public void StartBatchDownloadCommandValidator_ShouldRejectInvalidUrls()
    {
        var validator = new StartBatchDownloadCommandValidator();

        var invalidUrlReq = validator.Validate(new StartBatchDownloadCommand(new BatchDownloadRequest
        {
            Urls = new List<string> { "not-a-valid-url", "ftp://invalid-scheme.com" }
        }));

        invalidUrlReq.IsValid.Should().BeFalse();
        invalidUrlReq.Errors.Should().Contain(e => e.ErrorMessage.Contains("HTTP or HTTPS"));
    }

    [Fact]
    public void StartBatchDownloadCommandValidator_ShouldEnforceMaxBatchSize()
    {
        var validator = new StartBatchDownloadCommandValidator();
        var tooManyUrls = Enumerable.Range(1, 105).Select(i => $"https://youtube.com/watch?v={i:D11}").ToList();

        var result = validator.Validate(new StartBatchDownloadCommand(new BatchDownloadRequest { Urls = tooManyUrls }));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("cannot exceed 100"));
    }

    [Fact]
    public void StartBatchDownloadCommandValidator_ShouldPassForMixedValidUrls()
    {
        var validator = new StartBatchDownloadCommandValidator();
        var validReq = new BatchDownloadRequest
        {
            Urls = new List<string>
            {
                "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                "https://www.facebook.com/watch?v=123456",
                "https://www.facebook.com/watch?v=789012",
                "https://www.instagram.com/reel/C-abc123xyz/",
                "https://www.instagram.com/p/C-def456uvw/",
                "https://www.instagram.com/reel/C-ghi789rst/"
            }
        };

        var result = validator.Validate(new StartBatchDownloadCommand(validReq));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task StartBatchDownloadCommandHandler_ShouldEnqueueBatchCorrectly()
    {
        var fakeManager = new FakeDownloadManagerWithBatch();
        var handler = new StartBatchDownloadCommandHandler(fakeManager);

        var request = new BatchDownloadRequest
        {
            Urls = new List<string>
            {
                "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                "https://www.facebook.com/watch?v=12345",
                "https://www.instagram.com/reel/C-abc123xyz/"
            },
            CategoryName = "SocialMedia"
        };

        var response = await handler.Handle(new StartBatchDownloadCommand(request), CancellationToken.None);

        response.Should().NotBeNull();
        response.BatchId.Should().NotBeEmpty();
        response.EnqueuedJobsCount.Should().Be(3);
        response.Jobs.Should().HaveCount(3);
        response.Jobs[0].Portal.Should().Be("YouTube");
        response.Jobs[1].Portal.Should().Be("Facebook");
        response.Jobs[2].Portal.Should().Be("Instagram");
    }

    [Fact]
    public async Task DownloadQueue_ShouldEnqueueAndReadJobsInOrder()
    {
        var queue = new DownloadQueue();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        await queue.EnqueueAsync(id1);
        await queue.EnqueueAsync(id2);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var received = new List<Guid>();

        await foreach (var id in queue.ReadAllAsync(cts.Token))
        {
            received.Add(id);
            if (received.Count == 2) break;
        }

        received.Should().ContainInOrder(id1, id2);
    }

    [Fact]
    public void ProbePlaylistQueryValidator_ShouldValidateUrl()
    {
        var validator = new ProbePlaylistQueryValidator();

        var empty = validator.Validate(new ProbePlaylistQuery(""));
        empty.IsValid.Should().BeFalse();

        var invalid = validator.Validate(new ProbePlaylistQuery("ftp://test.com/playlist"));
        invalid.IsValid.Should().BeFalse();

        var valid = validator.Validate(new ProbePlaylistQuery("https://www.youtube.com/playlist?list=PL12345"));
        valid.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task StartBatchDownloadCommandHandler_ShouldSupportAudioOnlyOption()
    {
        var fakeManager = new FakeDownloadManagerWithBatch();
        var handler = new StartBatchDownloadCommandHandler(fakeManager);

        var request = new BatchDownloadRequest
        {
            Urls = new List<string>
            {
                "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                "https://soundcloud.com/artist/track"
            },
            AudioOnly = true,
            CategoryName = "Podcasts"
        };

        var response = await handler.Handle(new StartBatchDownloadCommand(request), CancellationToken.None);

        response.Should().NotBeNull();
        response.EnqueuedJobsCount.Should().Be(2);
        response.Jobs.Should().AllSatisfy(j =>
        {
            j.MediaType.Should().Be("Audio");
            j.Format.Should().Be("Mp3");
        });
    }

    private class FakeDownloadManagerWithBatch : IDownloadManager
    {
        public Task<BatchDownloadResponseDto> StartBatchDownloadAsync(BatchDownloadRequest request, CancellationToken cancellationToken = default)
        {
            var batchId = Guid.NewGuid();
            var isAudio = request.AudioOnly || request.MediaType == OutputMediaType.Audio;
            var jobs = request.Urls.Select(url => new DownloadJobRecordDto
            {
                Id = Guid.NewGuid(),
                BatchId = batchId,
                Url = url,
                Portal = MediaPortalDetector.DetectPortal(url),
                Title = "Test Media",
                CategoryName = request.CategoryName,
                Status = DownloadStatus.Queued,
                MediaType = isAudio ? "Audio" : "Video",
                Format = isAudio ? "Mp3" : "Mp4"
            }).ToList();

            return Task.FromResult(new BatchDownloadResponseDto
            {
                BatchId = batchId,
                Status = DownloadStatus.Queued,
                EnqueuedJobsCount = jobs.Count,
                Jobs = jobs,
                Message = $"Queued {jobs.Count} jobs."
            });
        }

        public Task<DownloadJobDto> StartDownloadAsync(DownloadRequest request) => Task.FromResult(new DownloadJobDto());
        public Task<DownloadJobDto> StartDownloadAsync(string url, string? categoryName, string? quality) => Task.FromResult(new DownloadJobDto());
        public List<DownloadJobDto> GetJobs() => new();
        public bool CancelJob(Guid jobId) => true;
        public Task UpdateYtDlpAsync() => Task.CompletedTask;
        public Task<List<string>> GetAvailableQualitiesAsync(string url) => Task.FromResult(new List<string>());
        public Task<VideoProbeResultDto> ProbeVideoAsync(string url) => Task.FromResult(new VideoProbeResultDto());
        public Task<List<DownloadBatchDto>> GetBatchesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<DownloadBatchDto>());
        public Task<DownloadBatchDto?> GetBatchByIdAsync(Guid batchId, CancellationToken cancellationToken = default) => Task.FromResult<DownloadBatchDto?>(null);
        public Task<PlaylistProbeResultDto> ProbePlaylistAsync(string url, CancellationToken cancellationToken = default) => Task.FromResult(new PlaylistProbeResultDto());
        public Task<bool> CancelBatchAsync(Guid batchId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<int> CancelAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(5);
        public Task<int> ClearFinishedJobsAsync(CancellationToken cancellationToken = default) => Task.FromResult(3);
        public Task<bool> DeleteBatchAsync(Guid batchId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public bool IsJobCancelled(Guid jobId) => false;
    }

    [Fact]
    public async Task CancelBatchCommand_ShouldReturnSuccess()
    {
        var manager = new FakeDownloadManagerWithBatch();
        var handler = new CancelBatchCommandHandler(manager);
        var batchId = Guid.NewGuid();

        var result = await handler.Handle(new CancelBatchCommand(batchId), CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CancelAllDownloadsCommand_ShouldReturnCancelledCount()
    {
        var manager = new FakeDownloadManagerWithBatch();
        var handler = new CancelAllDownloadsCommandHandler(manager);

        var result = await handler.Handle(new CancelAllDownloadsCommand(), CancellationToken.None);

        result.Should().Be(5);
    }

    [Fact]
    public async Task ClearFinishedDownloadsCommand_ShouldReturnClearedCount()
    {
        var manager = new FakeDownloadManagerWithBatch();
        var handler = new ClearFinishedDownloadsCommandHandler(manager);

        var result = await handler.Handle(new ClearFinishedDownloadsCommand(), CancellationToken.None);

        result.Should().Be(3);
    }

    [Fact]
    public async Task DeleteBatchCommand_ShouldReturnSuccess()
    {
        var manager = new FakeDownloadManagerWithBatch();
        var handler = new DeleteBatchCommandHandler(manager);
        var batchId = Guid.NewGuid();

        var result = await handler.Handle(new DeleteBatchCommand(batchId), CancellationToken.None);

        result.Should().BeTrue();
    }
}
