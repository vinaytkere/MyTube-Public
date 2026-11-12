using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MyTube.Backend.Application;
using MyTube.Backend.Application.Features.Downloads;
using MyTube.Backend.Application.Features.Downloads.Commands;
using MyTube.Backend.Application.Features.Downloads.Queries;
using MyTube.Backend.Domain;
using Xunit;

namespace MyTube.Backend.Tests;

public class CqrsDownloadTests
{
    private class FakeDownloadManager : IDownloadManager
    {
        private readonly List<DownloadJobDto> _jobs = new();

        public void AddJob(DownloadJobDto job) => _jobs.Add(job);

        public Task<DownloadJobDto> StartDownloadAsync(DownloadRequest request)
        {
            var job = new DownloadJobDto
            {
                Id = Guid.NewGuid(),
                Url = request.Url,
                CategoryName = request.CategoryName ?? "General",
                Status = DownloadStatus.Downloading
            };
            _jobs.Add(job);
            return Task.FromResult(job);
        }

        public Task<DownloadJobDto> StartDownloadAsync(string url, string? categoryName, string? quality)
            => StartDownloadAsync(new DownloadRequest { Url = url, CategoryName = categoryName, Quality = quality });

        public List<DownloadJobDto> GetJobs() => _jobs;

        public bool CancelJob(Guid jobId)
        {
            var job = _jobs.Find(j => j.Id == jobId);
            if (job == null) return false;
            job.Status = DownloadStatus.Cancelled;
            return true;
        }

        public Task<VideoProbeResultDto> ProbeVideoAsync(string url)
        {
            return Task.FromResult(new VideoProbeResultDto
            {
                Title = "Test Video",
                DurationSeconds = 120,
                Qualities = new List<string> { "1080p", "720p" }
            });
        }

        public Task<List<string>> GetAvailableQualitiesAsync(string url)
        {
            return Task.FromResult(new List<string> { "1080p", "720p", "480p" });
        }

        public Task UpdateYtDlpAsync() => Task.CompletedTask;

        public Task<BatchDownloadResponseDto> StartBatchDownloadAsync(BatchDownloadRequest request, CancellationToken cancellationToken = default)
        {
            var batchId = Guid.NewGuid();
            var jobs = new List<DownloadJobRecordDto>();
            foreach (var url in request.Urls)
            {
                var job = new DownloadJobRecordDto
                {
                    Id = Guid.NewGuid(),
                    BatchId = batchId,
                    Url = url,
                    Portal = "Generic",
                    Title = "Test Video",
                    Status = DownloadStatus.Queued
                };
                jobs.Add(job);
            }

            return Task.FromResult(new BatchDownloadResponseDto
            {
                BatchId = batchId,
                Status = DownloadStatus.Queued,
                EnqueuedJobsCount = jobs.Count,
                Jobs = jobs,
                Message = $"Enqueued {jobs.Count} jobs."
            });
        }

        public Task<List<DownloadBatchDto>> GetBatchesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<DownloadBatchDto>());

        public Task<DownloadBatchDto?> GetBatchByIdAsync(Guid batchId, CancellationToken cancellationToken = default)
            => Task.FromResult<DownloadBatchDto?>(null);

        public Task<PlaylistProbeResultDto> ProbePlaylistAsync(string url, CancellationToken cancellationToken = default)
            => Task.FromResult(new PlaylistProbeResultDto
            {
                Title = "Test Playlist",
                Items = new List<PlaylistProbeItemDto>
                {
                    new() { Id = "vid1", Title = "Video 1", Url = "https://youtube.com/watch?v=vid1" },
                    new() { Id = "vid2", Title = "Video 2", Url = "https://youtube.com/watch?v=vid2" }
                }
            });

        public Task<bool> CancelBatchAsync(Guid batchId, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<int> CancelAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);

        public Task<int> ClearFinishedJobsAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);

        public Task<bool> DeleteBatchAsync(Guid batchId, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public bool IsJobCancelled(Guid jobId) => false;
    }

    private class FakeDownloaderSettingsStore : IDownloaderSettingsStore
    {
        private DownloaderSettingsDto _settings = new();

        public DownloaderSettingsDto GetSettings() => _settings;

        public Task UpdateSettingsAsync(DownloaderSettingsDto settings, CancellationToken cancellationToken = default)
        {
            _settings = settings;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void StartDownloadCommandValidator_ShouldValidateUrl()
    {
        var validator = new StartDownloadCommandValidator();

        var invalidEmpty = validator.Validate(new StartDownloadCommand(new DownloadRequest { Url = "" }));
        var invalidScheme = validator.Validate(new StartDownloadCommand(new DownloadRequest { Url = "ftp://example.com/video.mp4" }));
        var valid = validator.Validate(new StartDownloadCommand(new DownloadRequest { Url = "https://youtube.com/watch?v=12345" }));

        invalidEmpty.IsValid.Should().BeFalse();
        invalidScheme.IsValid.Should().BeFalse();
        valid.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task StartDownloadCommandHandler_ShouldStartJob()
    {
        var manager = new FakeDownloadManager();
        var handler = new StartDownloadCommandHandler(manager);

        var request = new DownloadRequest { Url = "https://youtube.com/watch?v=abc", CategoryName = "Gaming" };
        var job = await handler.Handle(new StartDownloadCommand(request), CancellationToken.None);

        job.Should().NotBeNull();
        job.Url.Should().Be("https://youtube.com/watch?v=abc");
        job.CategoryName.Should().Be("Gaming");
        job.Status.Should().Be(DownloadStatus.Downloading);
    }

    [Fact]
    public async Task CancelDownloadJobCommandHandler_ShouldCancelExistingJob()
    {
        var manager = new FakeDownloadManager();
        var jobId = Guid.NewGuid();
        manager.AddJob(new DownloadJobDto { Id = jobId, Url = "https://test.com", Status = DownloadStatus.Downloading });

        var handler = new CancelDownloadJobCommandHandler(manager);

        var success = await handler.Handle(new CancelDownloadJobCommand(jobId), CancellationToken.None);
        var notFound = await handler.Handle(new CancelDownloadJobCommand(Guid.NewGuid()), CancellationToken.None);

        success.Should().BeTrue();
        notFound.Should().BeFalse();
    }

    [Fact]
    public async Task GetDownloadJobFileQueryHandler_ShouldReturnFileDetails_WhenFileExists()
    {
        var tempFile = Path.GetTempFileName();
        var mp4File = Path.ChangeExtension(tempFile, ".mp4");
        File.Move(tempFile, mp4File);

        try
        {
            var manager = new FakeDownloadManager();
            var jobId = Guid.NewGuid();
            manager.AddJob(new DownloadJobDto { Id = jobId, FilePath = mp4File, Status = DownloadStatus.Completed });

            var handler = new GetDownloadJobFileQueryHandler(manager);
            var result = await handler.Handle(new GetDownloadJobFileQuery(jobId), CancellationToken.None);

            result.Should().NotBeNull();
            result!.ContentType.Should().Be("video/mp4");
            result.FileName.Should().Be(Path.GetFileName(mp4File));
        }
        finally
        {
            if (File.Exists(mp4File)) File.Delete(mp4File);
        }
    }

    [Fact]
    public async Task ProbeVideo_And_QualitiesQueries_ShouldReturnExpectedData()
    {
        var manager = new FakeDownloadManager();

        var probeHandler = new ProbeVideoQueryHandler(manager);
        var probeResult = await probeHandler.Handle(new ProbeVideoQuery("https://youtube.com/watch?v=xyz"), CancellationToken.None);

        probeResult.Title.Should().Be("Test Video");
        probeResult.Qualities.Should().Contain("1080p");

        var qualitiesHandler = new GetAvailableQualitiesQueryHandler(manager);
        var qualities = await qualitiesHandler.Handle(new GetAvailableQualitiesQuery("https://youtube.com/watch?v=xyz"), CancellationToken.None);

        qualities.Should().HaveCount(3);
    }

    [Fact]
    public async Task SettingsQuery_And_UpdateCommand_ShouldWorkTogether()
    {
        var store = new FakeDownloaderSettingsStore();

        var updateHandler = new UpdateDownloaderSettingsCommandHandler(store);
        await updateHandler.Handle(new UpdateDownloaderSettingsCommand(new DownloaderSettingsDto { ForceIpv4 = false, Xff = "custom" }), CancellationToken.None);

        var queryHandler = new GetDownloaderSettingsQueryHandler(store);
        var settings = await queryHandler.Handle(new GetDownloaderSettingsQuery(), CancellationToken.None);

        settings.ForceIpv4.Should().BeFalse();
        settings.Xff.Should().Be("custom");
    }
}
