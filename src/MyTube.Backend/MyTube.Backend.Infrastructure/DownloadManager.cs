using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application;
using MyTube.Backend.Application.Media;
using MyTube.Backend.Domain;
using MyTube.Backend.Infrastructure.Media;

namespace MyTube.Backend.Infrastructure;

public class DownloadManager : IDownloadManager
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IVideoScanner _videoScanner;
    private readonly IMediaToolRunner _toolRunner;
    private readonly IMediaProcessor _mediaProcessor;
    private readonly IMediaProbeService _probeService;
    private readonly IConfiguration _configuration;
    private readonly IDownloaderSettingsStore? _settingsStore;
    private readonly ILogger<DownloadManager> _logger;
    private readonly string _mediaRoot;
    private readonly string _stagingRoot;

    private readonly IDownloadQueue? _queue;
    private readonly IDownloadNotifier? _notifier;

    private readonly ConcurrentDictionary<Guid, DownloadJobDto> _jobs = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _jobCts = new();
    private readonly SemaphoreSlim _semaphore = new(2, 2);

    private static readonly Regex PercentRegex = new Regex(
        @"(?:\[download\]\s+|\b)(\d+(?:\.\d+)?)%",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SpeedRegex = new Regex(
        @"\bat\s+([~0-9.]+\s*[kKmMgGbB]i?B/s)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex EtaRegex = new Regex(
        @"\bETA\s+(\d+:\d+(?::\d+)?|\d+s|\S+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly HashSet<string> ValidMediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".webm", ".mov", ".avi", ".m4a", ".mp3", ".opus", ".wav", ".ogg", ".flac", ".aac"
    };

    public DownloadManager(
        IServiceScopeFactory scopeFactory,
        IVideoScanner videoScanner,
        IMediaToolRunner toolRunner,
        IMediaProcessor mediaProcessor,
        IMediaProbeService probeService,
        IConfiguration configuration,
        ILogger<DownloadManager> logger,
        IDownloaderSettingsStore? settingsStore = null,
        IDownloadQueue? queue = null,
        IDownloadNotifier? notifier = null)
    {
        _scopeFactory = scopeFactory;
        _videoScanner = videoScanner;
        _toolRunner = toolRunner;
        _mediaProcessor = mediaProcessor;
        _probeService = probeService;
        _configuration = configuration;
        _settingsStore = settingsStore;
        _logger = logger;
        _queue = queue;
        _notifier = notifier;

        _mediaRoot = configuration["Media:RootPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "media");
        _stagingRoot = Path.Combine(Path.GetTempPath(), "mytube_staging");



        try
        {
            Directory.CreateDirectory(_stagingRoot);
            CleanupStagingAndProcessingArtifacts();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up stale staging directory at startup: {Path}", _stagingRoot);
        }
    }

    // Retained for backwards compatibility with existing tests
    public class KeptRange
    {
        public TimeSpan Start { get; set; }
        public TimeSpan? End { get; set; }
    }

    public Task<DownloadJobDto> StartDownloadAsync(
        string url,
        string? categoryName,
        string? quality)
    {
        var isAudio = IsAudioQuality(quality);
        var req = new DownloadRequest
        {
            Url = url,
            CategoryName = categoryName,
            Quality = quality,
            MediaType = isAudio ? OutputMediaType.Audio : OutputMediaType.Video,
            Format = isAudio ? OutputFormat.Mp3 : OutputFormat.Mp4
        };

        return StartDownloadAsync(req);
    }

    public async Task<DownloadJobDto> StartDownloadAsync(DownloadRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
        {
            throw new ArgumentException("URL is required.", nameof(request.Url));
        }

        var jobId = Guid.NewGuid();
        var category = SanitizeCategoryName(request.CategoryName);

        var isAudio = request.MediaType == OutputMediaType.Audio || IsAudioQuality(request.Quality);
        var targetMediaType = isAudio ? OutputMediaType.Audio : OutputMediaType.Video;
        var targetFormat = request.Format ?? (isAudio ? OutputFormat.Mp3 : OutputFormat.Mp4);
        if (isAudio && targetFormat == OutputFormat.Mp4)
        {
            targetFormat = OutputFormat.Mp3;
        }

        // When downloading in audio mode, explicitly disable subtitles
        if (isAudio)
        {
            request.IncludeSubtitles = false;
        }

        var job = new DownloadJobDto
        {
            Id = jobId,
            Url = request.Url,
            CategoryName = category,
            Status = DownloadStatus.Queued,
            Phase = "Queued",
            CreatedAt = DateTime.UtcNow,
            MediaType = targetMediaType,
            Format = targetFormat,
            IncludeSubtitles = !isAudio && request.IncludeSubtitles,
            SubtitleLanguage = !isAudio && request.IncludeSubtitles ? request.SubtitleLanguage : null,
            AudioLanguage = request.AudioLanguage
        };

        var cts = new CancellationTokenSource();
        _jobCts[jobId] = cts;
        _jobs[jobId] = job;

        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessDownloadJobAsync(jobId, request, cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled top-level exception in ProcessDownloadJobAsync for {JobId}", jobId);
                await FailJobAsync(job, ex, null, null);
            }
        });

        return job;
    }

    public List<DownloadJobDto> GetJobs()
    {
        return _jobs.Values.OrderByDescending(j => j.CreatedAt).ToList();
    }

    public bool IsJobCancelled(Guid jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            return job.Status == DownloadStatus.Cancelled;
        }
        return false;
    }

    public bool CancelJob(Guid jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Status = DownloadStatus.Cancelled;
            job.Phase = "Cancelled by user";

            if (_jobCts.TryRemove(jobId, out var cts))
            {
                try { cts.Cancel(); } catch { }
            }

            _ = CleanupFailedDownloadAsync(job.FilePath, job.Url);

            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<VideoDbContext>();
                    var record = await db.DownloadJobs.FirstOrDefaultAsync(j => j.Id == jobId);
                    if (record != null)
                    {
                        record.Status = DownloadStatus.Cancelled;
                        record.Phase = "Cancelled by user";
                        record.CompletedAt = DateTime.UtcNow;
                        await db.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to persist cancellation for job {JobId}", jobId);
                }
            });

            if (_notifier != null)
            {
                _ = _notifier.NotifyJobProgressAsync(new DownloadJobProgressNotification(
                    job.Id,
                    job.BatchId ?? Guid.Empty,
                    job.Title,
                    DownloadStatus.Cancelled,
                    job.Phase,
                    job.Progress,
                    string.Empty,
                    string.Empty));
            }

            return true;
        }

        return false;
    }

    public async Task<bool> CancelBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var memoryJobs = _jobs.Values.Where(j => j.BatchId == batchId).ToList();
        foreach (var j in memoryJobs)
        {
            if (j.Status != DownloadStatus.Completed && j.Status != DownloadStatus.Cancelled)
            {
                CancelJob(j.Id);
            }
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VideoDbContext>();

        var batch = await db.DownloadBatches
            .Include(b => b.Jobs)
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);

        if (batch != null)
        {
            batch.Status = DownloadStatus.Cancelled;
            batch.CompletedAt = DateTime.UtcNow;

            foreach (var j in batch.Jobs)
            {
                if (j.Status != DownloadStatus.Completed && j.Status != DownloadStatus.Cancelled)
                {
                    j.Status = DownloadStatus.Cancelled;
                    j.Phase = "Cancelled by user";
                    j.CompletedAt = DateTime.UtcNow;

                    if (_jobCts.TryRemove(j.Id, out var cts))
                    {
                        try { cts.Cancel(); } catch { }
                    }
                }
            }

            await db.SaveChangesAsync(cancellationToken);

            if (_notifier != null)
            {
                await _notifier.NotifyBatchUpdatedAsync(new DownloadBatchUpdatedNotification(
                    batch.Id,
                    batch.Status,
                    batch.CompletedItems,
                    batch.FailedItems,
                    batch.TotalItems), cancellationToken);
            }

            return true;
        }

        return memoryJobs.Count > 0;
    }

    public async Task<int> CancelAllAsync(CancellationToken cancellationToken = default)
    {
        int cancelled = 0;

        _queue?.Clear();

        foreach (var kvp in _jobCts)
        {
            try { kvp.Value.Cancel(); } catch { }
        }
        _jobCts.Clear();

        foreach (var job in _jobs.Values)
        {
            if (job.Status != DownloadStatus.Completed && job.Status != DownloadStatus.Cancelled)
            {
                job.Status = DownloadStatus.Cancelled;
                job.Phase = "Cancelled by user";
                cancelled++;

                if (_notifier != null)
                {
                    _ = _notifier.NotifyJobProgressAsync(new DownloadJobProgressNotification(
                        job.Id,
                        job.BatchId ?? Guid.Empty,
                        job.Title,
                        DownloadStatus.Cancelled,
                        "Cancelled by user",
                        job.Progress,
                        string.Empty,
                        string.Empty));
                }
            }
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VideoDbContext>();

            var activeBatches = await db.DownloadBatches
                .Include(b => b.Jobs)
                .Where(b => b.Status != DownloadStatus.Completed && b.Status != DownloadStatus.Cancelled)
                .ToListAsync(cancellationToken);

            foreach (var b in activeBatches)
            {
                b.Status = DownloadStatus.Cancelled;
                b.CompletedAt = DateTime.UtcNow;
                foreach (var j in b.Jobs)
                {
                    if (j.Status != DownloadStatus.Completed && j.Status != DownloadStatus.Cancelled)
                    {
                        j.Status = DownloadStatus.Cancelled;
                        j.Phase = "Cancelled by user";
                        j.CompletedAt = DateTime.UtcNow;
                    }
                }

                if (_notifier != null)
                {
                    _ = _notifier.NotifyBatchUpdatedAsync(new DownloadBatchUpdatedNotification(
                        b.Id,
                        b.Status,
                        b.CompletedItems,
                        b.FailedItems,
                        b.TotalItems), cancellationToken);
                }
            }

            var orphanJobs = await db.DownloadJobs
                .Where(j => j.Status != DownloadStatus.Completed && j.Status != DownloadStatus.Cancelled)
                .ToListAsync(cancellationToken);

            foreach (var j in orphanJobs)
            {
                j.Status = DownloadStatus.Cancelled;
                j.Phase = "Cancelled by user";
                j.CompletedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist bulk cancellation in database");
        }

        return cancelled;
    }

    public async Task<int> ClearFinishedJobsAsync(CancellationToken cancellationToken = default)
    {
        var finishedJobs = _jobs.Values
            .Where(j => j.Status == DownloadStatus.Completed || j.Status == DownloadStatus.Cancelled || j.Status == DownloadStatus.Failed || j.Status == DownloadStatus.DownloadFailed || j.Status == DownloadStatus.ProcessingFailed || j.Status == DownloadStatus.ValidationFailed || j.Status == DownloadStatus.PublishFailed)
            .ToList();

        foreach (var j in finishedJobs)
        {
            _jobs.TryRemove(j.Id, out _);
            _jobCts.TryRemove(j.Id, out _);
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VideoDbContext>();

            var finishedBatches = await db.DownloadBatches
                .Where(b => b.Status == DownloadStatus.Completed || b.Status == DownloadStatus.Cancelled || b.Status == DownloadStatus.Failed)
                .ToListAsync(cancellationToken);

            if (finishedBatches.Count > 0)
            {
                db.DownloadBatches.RemoveRange(finishedBatches);
                await db.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean finished batches from database");
        }

        return finishedJobs.Count;
    }

    public async Task<bool> DeleteBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        await CancelBatchAsync(batchId, cancellationToken);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VideoDbContext>();

        var batch = await db.DownloadBatches
            .Include(b => b.Jobs)
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);

        if (batch != null)
        {
            foreach (var j in batch.Jobs)
            {
                _jobs.TryRemove(j.Id, out _);
                _jobCts.TryRemove(j.Id, out _);
            }
            db.DownloadBatches.Remove(batch);
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var memJobs = _jobs.Values.Where(j => j.BatchId == batchId).ToList();
            foreach (var j in memJobs)
            {
                _jobs.TryRemove(j.Id, out _);
                _jobCts.TryRemove(j.Id, out _);
            }
        }

        return true;
    }

    public async Task<List<string>> GetAvailableQualitiesAsync(string url)
    {
        var probe = await _probeService.ProbeUrlAsync(url);
        return probe.Qualities;
    }

    public async Task<VideoProbeResultDto> ProbeVideoAsync(string url)
    {
        return await _probeService.ProbeUrlAsync(url);
    }

    public async Task UpdateYtDlpAsync()
    {
        try
        {
            var res = await _toolRunner.RunToolAsync("yt-dlp", new[] { "-U" }, timeout: TimeSpan.FromMinutes(2));
            _logger.LogInformation("yt-dlp update result: Code {Code}, Stderr: {Err}", res.ExitCode, res.StandardError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run yt-dlp update command.");
        }
    }

    public async Task<PlaylistProbeResultDto> ProbePlaylistAsync(string url, CancellationToken cancellationToken = default)
    {
        return await _probeService.ProbePlaylistAsync(url, cancellationToken);
    }

    public async Task<BatchDownloadResponseDto> StartBatchDownloadAsync(
        BatchDownloadRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || request.Urls == null || request.Urls.Count == 0)
        {
            throw new ArgumentException("At least one URL must be provided in the batch request.", nameof(request));
        }

        var targetItems = new List<(string Url, string? Title)>();
        foreach (var rawUrl in request.Urls)
        {
            var normalized = MediaPortalDetector.NormalizeUrl(rawUrl);
            if (string.IsNullOrWhiteSpace(normalized)) continue;

            if (request.ExpandPlaylists && MediaPortalDetector.IsPlaylistUrl(normalized))
            {
                try
                {
                    var probe = await _probeService.ProbePlaylistAsync(normalized, cancellationToken);
                    if (probe.Items.Count > 0)
                    {
                        foreach (var item in probe.Items)
                        {
                            targetItems.Add((item.Url, item.Title));
                        }
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to expand playlist {Url}; queuing as single URL", normalized);
                }
            }

            targetItems.Add((normalized, null));
        }

        if (targetItems.Count == 0)
        {
            throw new ArgumentException("No valid URLs found in the batch request.", nameof(request));
        }

        var batchId = Guid.NewGuid();
        var category = SanitizeCategoryName(request.CategoryName);
        var isAudio = request.AudioOnly || request.MediaType == OutputMediaType.Audio || IsAudioQuality(request.Quality);
        var targetMediaType = isAudio ? OutputMediaType.Audio : OutputMediaType.Video;
        var targetFormat = request.Format ?? (isAudio ? OutputFormat.Mp3 : OutputFormat.Mp4);
        if (isAudio && targetFormat == OutputFormat.Mp4)
        {
            targetFormat = OutputFormat.Mp3;
        }

        var batch = new DownloadBatch
        {
            Id = batchId,
            CreatedAt = DateTime.UtcNow,
            CategoryName = category,
            TotalItems = targetItems.Count,
            CompletedItems = 0,
            FailedItems = 0,
            Status = DownloadStatus.Queued
        };

        var jobRecords = new List<DownloadJobRecord>();
        foreach (var item in targetItems)
        {
            var jobId = Guid.NewGuid();
            var portal = MediaPortalDetector.DetectPortal(item.Url);
            var title = !string.IsNullOrWhiteSpace(item.Title) ? item.Title : "Fetching video details...";

            var jobRecord = new DownloadJobRecord
            {
                Id = jobId,
                BatchId = batchId,
                Url = item.Url,
                Portal = portal,
                Title = title,
                CategoryName = category,
                Status = DownloadStatus.Queued,
                Phase = "Queued",
                Progress = 0,
                CreatedAt = DateTime.UtcNow,
                MediaType = targetMediaType.ToString(),
                Format = targetFormat.ToString(),
                Quality = request.Quality,
                IncludeSubtitles = request.IncludeSubtitles,
                SubtitleLanguage = request.SubtitleLanguage
            };
            jobRecords.Add(jobRecord);

            var jobDto = new DownloadJobDto
            {
                Id = jobId,
                BatchId = batchId,
                Url = item.Url,
                Title = title,
                CategoryName = category ?? "",
                Status = DownloadStatus.Queued,
                Phase = "Queued",
                Progress = 0,
                CreatedAt = jobRecord.CreatedAt,
                MediaType = targetMediaType,
                Format = targetFormat,
                IncludeSubtitles = request.IncludeSubtitles,
                SubtitleLanguage = request.SubtitleLanguage,
                AudioLanguage = request.AudioLanguage
            };
            _jobs[jobId] = jobDto;
        }

        batch.Jobs = jobRecords;

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VideoDbContext>();
            db.DownloadBatches.Add(batch);
            await db.SaveChangesAsync(cancellationToken);
        }

        if (_queue != null)
        {
            foreach (var j in jobRecords)
            {
                await _queue.EnqueueAsync(j.Id, cancellationToken);
            }
        }
        else
        {
            // Direct processing fallback if queue is not active
            foreach (var j in jobRecords)
            {
                var req = new DownloadRequest
                {
                    Url = j.Url,
                    CategoryName = j.CategoryName,
                    Quality = j.Quality,
                    MediaType = targetMediaType,
                    Format = targetFormat,
                    IncludeSubtitles = j.IncludeSubtitles,
                    SubtitleLanguage = j.SubtitleLanguage,
                    AudioLanguage = request.AudioLanguage
                };
                var cts = new CancellationTokenSource();
                _jobCts[j.Id] = cts;
                _ = Task.Run(async () =>
                {
                    try { await ProcessDownloadJobAsync(j.Id, req, cts.Token); }
                    catch (Exception ex) { _logger.LogError(ex, "Error processing job {Id}", j.Id); }
                });
            }
        }

        if (_notifier != null)
        {
            await _notifier.NotifyBatchUpdatedAsync(new DownloadBatchUpdatedNotification(
                batch.Id, batch.Status, batch.CompletedItems, batch.FailedItems, batch.TotalItems), cancellationToken);
        }

        return new BatchDownloadResponseDto
        {
            BatchId = batch.Id,
            Status = batch.Status,
            EnqueuedJobsCount = jobRecords.Count,
            Jobs = jobRecords.Select(MapToJobRecordDto).ToList(),
            Message = $"Queued {jobRecords.Count} download job(s) in batch."
        };
    }

    public async Task<List<DownloadBatchDto>> GetBatchesAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VideoDbContext>();

        var batches = await db.DownloadBatches
            .AsNoTracking()
            .Include(b => b.Jobs)
            .OrderByDescending(b => b.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        return batches.Select(MapToBatchDto).ToList();
    }

    public async Task<DownloadBatchDto?> GetBatchByIdAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VideoDbContext>();

        var batch = await db.DownloadBatches
            .AsNoTracking()
            .Include(b => b.Jobs)
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);

        return batch != null ? MapToBatchDto(batch) : null;
    }

    public async Task ExecuteQueuedJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VideoDbContext>();

        var record = await db.DownloadJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (record == null)
        {
            _logger.LogWarning("Queued job {JobId} not found in database.", jobId);
            return;
        }

        if (record.Status == DownloadStatus.Cancelled)
        {
            _logger.LogInformation("Queued job {JobId} was cancelled; skipping execution.", jobId);
            return;
        }

        var isAudio = record.MediaType.Equals("Audio", StringComparison.OrdinalIgnoreCase);
        var req = new DownloadRequest
        {
            Url = record.Url,
            CategoryName = record.CategoryName,
            Quality = record.Quality,
            MediaType = isAudio ? OutputMediaType.Audio : OutputMediaType.Video,
            Format = Enum.TryParse<OutputFormat>(record.Format, true, out var f) ? f : (isAudio ? OutputFormat.Mp3 : OutputFormat.Mp4),
            IncludeSubtitles = record.IncludeSubtitles,
            SubtitleLanguage = record.SubtitleLanguage
        };

        if (!_jobs.ContainsKey(jobId))
        {
            _jobs[jobId] = new DownloadJobDto
            {
                Id = jobId,
                Url = record.Url,
                Title = record.Title,
                CategoryName = record.CategoryName ?? "",
                Status = DownloadStatus.Queued,
                Phase = "Queued",
                Progress = 0,
                CreatedAt = record.CreatedAt,
                MediaType = req.MediaType,
                Format = req.Format ?? OutputFormat.Mp4,
                IncludeSubtitles = record.IncludeSubtitles,
                SubtitleLanguage = record.SubtitleLanguage
            };
        }

        var cts = new CancellationTokenSource();
        _jobCts[jobId] = cts;
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

        try
        {
            await ProcessDownloadJobAsync(jobId, req, linkedCts.Token);

            record.Status = DownloadStatus.Completed;
            record.Phase = "Complete";
            record.Progress = 100;
            record.Speed = string.Empty;
            record.Eta = string.Empty;
            record.CompletedAt = DateTime.UtcNow;
            if (_jobs.TryGetValue(jobId, out var finishedJob))
            {
                record.Title = finishedJob.Title;
                record.FilePath = finishedJob.FilePath;
            }

            var batch = await db.DownloadBatches.FirstOrDefaultAsync(b => b.Id == record.BatchId, cancellationToken);
            if (batch != null)
            {
                batch.CompletedItems++;
                if (batch.CompletedItems + batch.FailedItems >= batch.TotalItems)
                {
                    batch.Status = batch.FailedItems > 0 ? DownloadStatus.Failed : DownloadStatus.Completed;
                    batch.CompletedAt = DateTime.UtcNow;
                }
            }

            await db.SaveChangesAsync(CancellationToken.None);

            if (_notifier != null)
            {
                await _notifier.NotifyJobCompletedAsync(new DownloadJobCompletedNotification(
                    record.Id, record.BatchId, record.Title, record.FilePath), cancellationToken);

                if (batch != null)
                {
                    await _notifier.NotifyBatchUpdatedAsync(new DownloadBatchUpdatedNotification(
                        batch.Id, batch.Status, batch.CompletedItems, batch.FailedItems, batch.TotalItems), cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing queued job {JobId}", jobId);

            record.Status = DownloadStatus.Failed;
            record.ErrorMessage = ex.Message;
            record.CompletedAt = DateTime.UtcNow;

            var batch = await db.DownloadBatches.FirstOrDefaultAsync(b => b.Id == record.BatchId, cancellationToken);
            if (batch != null)
            {
                batch.FailedItems++;
                if (batch.CompletedItems + batch.FailedItems >= batch.TotalItems)
                {
                    batch.Status = DownloadStatus.Failed;
                    batch.CompletedAt = DateTime.UtcNow;
                }
            }

            await db.SaveChangesAsync(CancellationToken.None);

            if (_notifier != null)
            {
                await _notifier.NotifyJobFailedAsync(new DownloadJobFailedNotification(
                    record.Id, record.BatchId, record.Title, record.ErrorMessage ?? "Download failed"), cancellationToken);

                if (batch != null)
                {
                    await _notifier.NotifyBatchUpdatedAsync(new DownloadBatchUpdatedNotification(
                        batch.Id, batch.Status, batch.CompletedItems, batch.FailedItems, batch.TotalItems), cancellationToken);
                }
            }
        }
        finally
        {
            _jobCts.TryRemove(jobId, out _);
        }
    }

    private void NotifyJobStateChanged(DownloadJobDto job)
    {
        if (_notifier != null)
        {
            _ = _notifier.NotifyJobProgressAsync(new DownloadJobProgressNotification(
                job.Id,
                Guid.Empty,
                job.Title,
                job.Status,
                job.Phase,
                job.Progress,
                job.Speed,
                job.Eta));
        }
    }

    private static DownloadBatchDto MapToBatchDto(DownloadBatch b) => new()
    {
        Id = b.Id,
        CreatedAt = b.CreatedAt,
        CompletedAt = b.CompletedAt,
        CategoryName = b.CategoryName,
        TotalItems = b.TotalItems,
        CompletedItems = b.CompletedItems,
        FailedItems = b.FailedItems,
        Status = b.Status,
        Jobs = b.Jobs.Select(MapToJobRecordDto).ToList()
    };

    private static DownloadJobRecordDto MapToJobRecordDto(DownloadJobRecord j) => new()
    {
        Id = j.Id,
        BatchId = j.BatchId,
        Url = j.Url,
        Portal = j.Portal,
        Title = j.Title,
        CategoryName = j.CategoryName,
        Status = j.Status,
        Phase = j.Phase,
        Progress = j.Progress,
        Speed = j.Speed,
        Eta = j.Eta,
        ErrorMessage = j.ErrorMessage,
        RetryCount = j.RetryCount,
        CreatedAt = j.CreatedAt,
        CompletedAt = j.CompletedAt,
        FilePath = j.FilePath,
        MediaType = j.MediaType,
        Format = j.Format,
        Quality = j.Quality,
        IncludeSubtitles = j.IncludeSubtitles,
        SubtitleLanguage = j.SubtitleLanguage
    };

    private async Task ProcessDownloadJobAsync(
        Guid jobId,
        DownloadRequest request,
        CancellationToken cancellationToken)
    {
        if (!_jobs.TryGetValue(jobId, out var job)) return;

        await _semaphore.WaitAsync(cancellationToken);
        string? stagingDir = null;
        string? targetPath = null;
        string? tempProcessingPath = null;
        var swTotal = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (job.Status == DownloadStatus.Cancelled) return;

            _logger.LogInformation("Job {JobId}: [BREAKPOINT:1_Init] Started download lifecycle for URL: {Url}, Requested Quality: {Quality}, Format: {Format}",
                jobId, request.Url, request.Quality, job.Format);

            // Check disk space on both media root and staging
            EnsureSufficientDiskSpace(_mediaRoot, _stagingRoot, 1024L * 1024 * 1024);

            job.Status = DownloadStatus.Probing;
            job.Phase = "Probing source metadata...";
            job.Progress = 5;

            var swProbeStep = System.Diagnostics.Stopwatch.StartNew();
            VideoProbeResultDto probe;
            try
            {
                probe = await _probeService.ProbeUrlAsync(request.Url, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Job {JobId}: Initial probe failed for {Url}. Proceeding with fallback metadata.", jobId, request.Url);
                probe = new VideoProbeResultDto { Title = "" };
            }
            swProbeStep.Stop();
            _logger.LogInformation("Job {JobId}: [BREAKPOINT:2_ProbeSource] Completed in {ElapsedMs}ms. Title: '{Title}', Duration: {Duration}s, Qualities count: {Count}",
                jobId, swProbeStep.ElapsedMilliseconds, probe.Title, probe.DurationSeconds, probe.Qualities.Count);

            var sourceDuration = TimeSpan.FromSeconds(probe.DurationSeconds.HasValue && probe.DurationSeconds.Value > 0 ? probe.DurationSeconds.Value : 0);
            if (sourceDuration > TimeSpan.Zero)
            {
                job.ExpectedDurationSeconds = sourceDuration.TotalSeconds;
            }

            var isAudio = job.MediaType == OutputMediaType.Audio;
            var extension = job.Format switch
            {
                OutputFormat.Mp3 => "mp3",
                OutputFormat.M4a => "m4a",
                OutputFormat.Opus => "opus",
                OutputFormat.Wav => "wav",
                _ => "mp4"
            };

            // Containment check on target folder
            var targetFolder = string.IsNullOrWhiteSpace(job.CategoryName) || string.Equals(job.CategoryName, "Downloads", StringComparison.OrdinalIgnoreCase)
                ? (isAudio ? Path.Combine(_mediaRoot, "Downloads", "Audio") : Path.Combine(_mediaRoot, "Downloads"))
                : Path.Combine(_mediaRoot, "Downloads", job.CategoryName);
            targetFolder = ValidateAndContainPath(_mediaRoot, targetFolder);
            Directory.CreateDirectory(targetFolder);

            // Resolve base filename: reuse probed title if available to eliminate a redundant yt-dlp process
            string? rawName = null;
            if (!string.IsNullOrWhiteSpace(probe.Title))
            {
                rawName = probe.Title;
                _logger.LogInformation("Job {JobId}: Reusing probed title '{Title}' for target filename (avoided redundant yt-dlp process)", jobId, probe.Title);
            }
            else
            {
                // Fallback 1: only invoke yt-dlp filename extraction if the initial probe did not yield a title
                var outputTemplate = "%(title).80s.%(ext)s";
                var (resolvedPath, resolveError) = await GetTargetFilePathAsync(request.Url, outputTemplate, cancellationToken);
                if (!string.IsNullOrWhiteSpace(resolvedPath))
                {
                    rawName = Path.GetFileNameWithoutExtension(resolvedPath);
                }

                // Fallback 2: if yt-dlp filename extraction was also empty, extract title from URL slug or generate a clean fallback
                if (string.IsNullOrWhiteSpace(rawName))
                {
                    try
                    {
                        var uri = new Uri(request.Url);
                        var lastSegment = uri.AbsolutePath.Trim('/').Split('/').LastOrDefault();
                        if (!string.IsNullOrWhiteSpace(lastSegment) && !lastSegment.Contains('?'))
                        {
                            var cleaned = System.Text.RegularExpressions.Regex.Replace(lastSegment, @"[-_+.]+", " ").Trim();
                            if (cleaned.Length > 2)
                            {
                                rawName = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(cleaned);
                            }
                        }
                    }
                    catch { }
                }

                if (string.IsNullOrWhiteSpace(rawName))
                {
                    rawName = $"video_{jobId:N}"[..14];
                }
            }

            targetPath = SafePathHelper.SanitizeDestinationPath(targetFolder, rawName, extension);
            targetPath = ValidateAndContainPath(_mediaRoot, targetPath);
            targetPath = EnsureUniqueFilePath(targetPath);
            job.Title = Path.GetFileNameWithoutExtension(targetPath);
            _logger.LogInformation("Job {JobId}: [BREAKPOINT:3_ResolvedFilename] Final filename: '{Title}', Target path: '{Path}'",
                jobId, job.Title, targetPath);

            // Isolated staging folder per job
            // Isolated staging folders per job (staging/source/{jobId} and staging/processed/{jobId})
            stagingDir = Path.Combine(_stagingRoot, "jobs", jobId.ToString("N"));
            var stagingSourceDir = Path.Combine(_stagingRoot, "source", jobId.ToString("N"));
            var stagingProcessedDir = Path.Combine(_stagingRoot, "processed", jobId.ToString("N"));
            Directory.CreateDirectory(stagingSourceDir);
            Directory.CreateDirectory(stagingProcessedDir);

            tempProcessingPath = Path.Combine(stagingProcessedDir, $"output_{jobId:N}.{extension}");

            _logger.LogInformation("Job {JobId}: Using yt-dlp download + FFmpeg processing", jobId);

            // Phase 1: Download full source with yt-dlp
            job.Status = DownloadStatus.Downloading;
            job.Phase = isAudio ? "Downloading full source audio..." : "Downloading full source video...";
            job.Progress = 15;

            var sourceTemplate = Path.Combine(stagingSourceDir, $"source_{jobId:N}.%(ext)s");
            var shouldWriteAutoSubs = request.IncludeSubtitles &&
                (probe?.SubtitleTracks == null ||
                 probe.SubtitleTracks.Count == 0 ||
                 probe.SubtitleTracks.Any(t => t.IsAutoGenerated &&
                    (string.IsNullOrWhiteSpace(request.SubtitleLanguage) ||
                     request.SubtitleLanguage.Equals("auto", StringComparison.OrdinalIgnoreCase) ||
                     request.SubtitleLanguage.Contains("-orig", StringComparison.OrdinalIgnoreCase) ||
                     request.SubtitleLanguage.Equals(t.Code, StringComparison.OrdinalIgnoreCase))));

            var dlArgs = BuildYtDlpArguments(
                request.Url, 
                sourceTemplate, 
                request.Quality, 
                null, 
                isAudio, 
                request.IncludeSubtitles, 
                request.SubtitleLanguage, 
                includeProxy: true, 
                targetFormat: job.Format, 
                audioLanguage: request.AudioLanguage,
                useAria2c: true,
                probe: probe,
                writeAutoSubs: shouldWriteAutoSubs);

            _logger.LogInformation("Job {JobId}: [BREAKPOINT:4_YtDlpDownloadStart] Executing yt-dlp with {ArgCount} arguments. Quality: {Quality}, IsAudio: {IsAudio}, Format: {Format}, AudioLang: {AudioLang}, IncludeSubtitles: {IncludeSubtitles}, SubtitleLang: {SubtitleLang}",
                jobId, dlArgs.Count, request.Quality, isAudio, job.Format, request.AudioLanguage ?? "(default)", request.IncludeSubtitles, request.SubtitleLanguage ?? "(auto)");
            var dlRun = await RunYtDlpDownloadAsync(jobId, job, dlArgs, 0.55, 15.0, cancellationToken);

            var settings = _settingsStore?.GetSettings();
            var hasProxy = !string.IsNullOrWhiteSpace(settings?.Proxy) || !string.IsNullOrWhiteSpace(_configuration?["Media:Proxy"]);

            // 1. If download failed with aria2c network/socket error or exit code 1, retry with yt-dlp native downloader
            if (dlRun.ExitCode != 0 && IsAria2cFailure(dlRun.StandardError, dlRun.StandardOutput, job.ErrorMessage))
            {
                _logger.LogWarning("Job {JobId}: aria2c download failed ({Err}). Retrying with native yt-dlp downloader...", jobId, job.ErrorMessage);
                job.Phase = isAudio ? "Retrying audio download with native downloader..." : "Retrying video download with native downloader...";
                job.ErrorMessage = null;
                var nativeDlArgs = BuildYtDlpArguments(
                    request.Url, sourceTemplate, request.Quality, null, isAudio,
                    request.IncludeSubtitles, request.SubtitleLanguage, includeProxy: !hasProxy,
                    targetFormat: job.Format, audioLanguage: request.AudioLanguage,
                    useAria2c: false, probe: probe, writeAutoSubs: shouldWriteAutoSubs);
                dlRun = await RunYtDlpDownloadAsync(jobId, job, nativeDlArgs, 0.55, 15.0, cancellationToken);
            }

            // 2. If download failed and a proxy was active, retry without proxy
            if (dlRun.ExitCode != 0 && hasProxy)
            {
                _logger.LogWarning("Job {JobId}: Download failed with proxy ({Err}). Retrying directly without proxy...", jobId, job.ErrorMessage);
                job.Phase = isAudio ? "Retrying audio download without proxy..." : "Retrying video download without proxy...";
                job.ErrorMessage = null;
                var directDlArgs = BuildYtDlpArguments(
                    request.Url, sourceTemplate, request.Quality, null, isAudio,
                    request.IncludeSubtitles, request.SubtitleLanguage, includeProxy: false,
                    targetFormat: job.Format, audioLanguage: request.AudioLanguage,
                    useAria2c: true, probe: probe, writeAutoSubs: shouldWriteAutoSubs);
                dlRun = await RunYtDlpDownloadAsync(jobId, job, directDlArgs, 0.55, 15.0, cancellationToken);
            }

            // 3. If download failed with subtitle rate limit (429) or subtitle error:
            // Tier A: retry with creator subtitles only (disabling auto-subs which trigger 429)
            if (dlRun.ExitCode != 0 && request.IncludeSubtitles && IsSubtitleOr429Failure(dlRun.StandardError, dlRun.StandardOutput, job.ErrorMessage))
            {
                _logger.LogWarning("Job {JobId}: Subtitle download was rate-limited or failed ({Err}). Retrying media download with creator subtitles only...", jobId, job.ErrorMessage);
                job.Phase = isAudio ? "Retrying audio download with creator subtitles..." : "Retrying video download with creator subtitles...";
                job.ErrorMessage = null;
                var creatorSubDlArgs = BuildYtDlpArguments(
                    request.Url, sourceTemplate, request.Quality, null, isAudio,
                    includeSubtitles: true, request.SubtitleLanguage, includeProxy: !hasProxy,
                    targetFormat: job.Format, audioLanguage: request.AudioLanguage,
                    useAria2c: true, probe: probe, writeAutoSubs: false);
                dlRun = await RunYtDlpDownloadAsync(jobId, job, creatorSubDlArgs, 0.55, 15.0, cancellationToken);

                // Tier B: if still failing on subtitles, download media without subtitles
                if (dlRun.ExitCode != 0 && IsSubtitleOr429Failure(dlRun.StandardError, dlRun.StandardOutput, job.ErrorMessage))
                {
                    _logger.LogWarning("Job {JobId}: Subtitle download still failed ({Err}). Retrying media download without subtitles...", jobId, job.ErrorMessage);
                    job.Phase = isAudio ? "Retrying audio download without subtitles..." : "Retrying video download without subtitles...";
                    job.ErrorMessage = null;
                    var noSubDlArgs = BuildYtDlpArguments(
                        request.Url, sourceTemplate, request.Quality, null, isAudio,
                        includeSubtitles: false, null, includeProxy: !hasProxy,
                        targetFormat: job.Format, audioLanguage: request.AudioLanguage,
                        useAria2c: true, probe: probe, writeAutoSubs: false);
                    dlRun = await RunYtDlpDownloadAsync(jobId, job, noSubDlArgs, 0.55, 15.0, cancellationToken);
                }
            }

            // 4. If download failed with HTTP 403 Forbidden or bot detection, retry with alternate player client and clean cache
            if (dlRun.ExitCode != 0 && IsForbiddenOrBotBlocked(dlRun.StandardError, job.ErrorMessage))
            {
                _logger.LogWarning("Job {JobId}: Download failed with HTTP 403 Forbidden / bot check ({Err}). Retrying with fallback player client (mweb,tv,web) and clean cache...", jobId, job.ErrorMessage);
                job.Phase = isAudio ? "Retrying audio download with fallback player client..." : "Retrying video download with fallback player client...";
                job.ErrorMessage = null;
                var fallback403Args = BuildYtDlpArguments(
                    request.Url,
                    sourceTemplate,
                    request.Quality,
                    null,
                    isAudio,
                    includeSubtitles: false,
                    null,
                    includeProxy: !hasProxy,
                    targetFormat: job.Format,
                    overrideExtractorArgs: "youtube:player_client=mweb,tv,web",
                    includeUserAgent: false,
                    clearCache: true,
                    audioLanguage: request.AudioLanguage,
                    useAria2c: true,
                    probe: probe);
                dlRun = await RunYtDlpDownloadAsync(jobId, job, fallback403Args, 0.55, 15.0, cancellationToken);

                // Second tier fallback: try android client with native headers
                if (dlRun.ExitCode != 0 && IsForbiddenOrBotBlocked(dlRun.StandardError, job.ErrorMessage))
                {
                    _logger.LogWarning("Job {JobId}: Second 403 retry with android client...", jobId);
                    job.Phase = isAudio ? "Retrying audio download with android client..." : "Retrying video download with android client...";
                    job.ErrorMessage = null;
                    var androidArgs = BuildYtDlpArguments(
                        request.Url,
                        sourceTemplate,
                        request.Quality,
                        null,
                        isAudio,
                        includeSubtitles: false,
                        null,
                        includeProxy: !hasProxy,
                        targetFormat: job.Format,
                        overrideExtractorArgs: "youtube:player_client=android",
                        includeUserAgent: false,
                        clearCache: false,
                        audioLanguage: request.AudioLanguage,
                        useAria2c: true,
                        probe: probe);
                    dlRun = await RunYtDlpDownloadAsync(jobId, job, androidArgs, 0.55, 15.0, cancellationToken);
                }
            }

            if (dlRun.ExitCode != 0)
            {
                var cleanDlError = ExtractMeaningfulYtDlpError(dlRun.StandardError, dlRun.StandardOutput);
                throw new MediaProcessingException(
                    $"Source download failed with exit code {dlRun.ExitCode}: {cleanDlError}",
                    "YTDLP_DOWNLOAD_FAILED",
                    "yt-dlp",
                    dlRun.ExitCode,
                    "Downloading",
                    jobId,
                    job.ErrorMessage ?? dlRun.StandardError ?? "yt-dlp download failed",
                    cleanDlError,
                    isRetryable: true,
                    commandArguments: dlArgs);
            }

            string? downloadedSource = null;

            // 1. Check detected destination from yt-dlp logs if valid file on disk
            if (!string.IsNullOrWhiteSpace(dlRun.DetectedDestination) && File.Exists(dlRun.DetectedDestination))
            {
                var fi = new FileInfo(dlRun.DetectedDestination);
                if (fi.Length > 0 && ValidMediaExtensions.Contains(Path.GetExtension(dlRun.DetectedDestination)))
                {
                    downloadedSource = dlRun.DetectedDestination;
                }
            }

            // 2. Search stagingSourceDir for expected prefix
            if (downloadedSource == null && Directory.Exists(stagingSourceDir))
            {
                downloadedSource = Directory.GetFiles(stagingSourceDir, $"source_{jobId:N}.*", SearchOption.AllDirectories)
                    .FirstOrDefault(f => ValidMediaExtensions.Contains(Path.GetExtension(f)) && new FileInfo(f).Length > 0);
            }

            // 3. Fallback: Search stagingSourceDir recursively for ANY valid media file
            if (downloadedSource == null && Directory.Exists(stagingSourceDir))
            {
                downloadedSource = Directory.GetFiles(stagingSourceDir, "*.*", SearchOption.AllDirectories)
                    .FirstOrDefault(f => ValidMediaExtensions.Contains(Path.GetExtension(f)) && new FileInfo(f).Length > 0);
            }

            // 4. Fallback: Search stagingDir (root job staging folder)
            if (downloadedSource == null && !string.IsNullOrWhiteSpace(stagingDir) && Directory.Exists(stagingDir))
            {
                downloadedSource = Directory.GetFiles(stagingDir, "*.*", SearchOption.AllDirectories)
                    .FirstOrDefault(f => ValidMediaExtensions.Contains(Path.GetExtension(f)) && new FileInfo(f).Length > 0);
            }

            if (downloadedSource == null)
            {
                var existingFiles = Directory.Exists(stagingSourceDir)
                    ? Directory.GetFiles(stagingSourceDir, "*.*", SearchOption.AllDirectories).Select(f => $"{Path.GetFileName(f)} ({new FileInfo(f).Length} bytes)").ToList()
                    : new List<string>();

                var filesSummary = existingFiles.Count > 0 ? string.Join(", ", existingFiles) : "directory is empty";
                var cleanDlError = ExtractMeaningfulYtDlpError(dlRun.StandardError);

                _logger.LogError("Job {JobId}: Downloaded source file not found. Files in staging: {Files}. yt-dlp stderr: {Stderr}",
                    jobId, filesSummary, dlRun.StandardError);

                throw new MediaProcessingException(
                    $"Downloaded source file not found in staging directory ({filesSummary}). {cleanDlError}",
                    "SOURCE_FILE_MISSING",
                    "yt-dlp",
                    dlRun.ExitCode,
                    "Downloading",
                    jobId,
                    $"Searched {stagingSourceDir}. Files: {filesSummary}",
                    $"Downloaded source media file could not be found on disk ({filesSummary}).",
                    isRetryable: false,
                    commandArguments: dlArgs);
            }

            job.Status = DownloadStatus.Downloaded;
            job.SourceFileSize = new FileInfo(downloadedSource).Length;
            _logger.LogInformation("Job {JobId}: [BREAKPOINT:6_StagingFileFound] Found downloaded source at '{Path}' ({SizeMb:F2} MB)",
                jobId, downloadedSource, job.SourceFileSize / (1024.0 * 1024.0));

            // Phase 2: Probe downloaded source locally with FFprobe
            job.Status = DownloadStatus.Probing;
            job.Phase = "Probing downloaded media file...";
            job.Progress = 70;

            var swProbe = System.Diagnostics.Stopwatch.StartNew();
            var sourceProbe = await _probeService.ProbeFileAsync(downloadedSource, cancellationToken);
            swProbe.Stop();
            _logger.LogInformation("Job {JobId}: [BREAKPOINT:7_FFprobeSource] Check completed in {ElapsedMs}ms. VideoCodec={VideoCodec}, AudioCodec={AudioCodec}, Resolution={Width}x{Height}",
                jobId, swProbe.ElapsedMilliseconds, sourceProbe.VideoCodec, sourceProbe.AudioCodec, sourceProbe.Width, sourceProbe.Height);

            if (!sourceProbe.IsReadable || sourceProbe.Classification == MediaClassification.Invalid)
            {
                throw new MediaProcessingException(
                    $"Source file {downloadedSource} is not a valid media file: {sourceProbe.ErrorMessage}",
                    "INVALID_SOURCE_MEDIA",
                    "ffprobe",
                    sourceProbe.ExitCode,
                    "Probing",
                    jobId,
                    sourceProbe.ErrorMessage ?? "Invalid source",
                    "Downloaded source media is corrupted or invalid.",
                    isRetryable: false,
                    inputPath: downloadedSource);
            }

            // Phase 3: Media processing (No-op, Stream-copy with fallback, or Filtergraph)
            job.Status = DownloadStatus.Processing;
            job.Phase = job.MediaType == OutputMediaType.Audio
                ? $"Converting audio to {job.Format}..."
                : "Processing media via FFmpeg...";
            job.Progress = 72;

            var processReq = new MediaProcessRequest
            {
                JobId = jobId,
                InputPath = downloadedSource,
                OutputPath = tempProcessingPath,
                OutputMediaType = job.MediaType,
                OutputFormat = job.Format,
                OriginalDuration = sourceDuration > TimeSpan.Zero ? sourceDuration : sourceProbe.Duration,
                ExpectedDuration = sourceDuration > TimeSpan.Zero ? sourceDuration : sourceProbe.Duration,
                AudioBitrate = request.AudioBitrate,
                Options = new MediaProcessingOptions
                {
                    AudioBitrate = request.AudioBitrate,
                    AudioSampleRate = request.AudioSampleRate,
                    AudioChannels = request.AudioChannels
                },
                OnProgressPercent = frac =>
                {
                    job.Progress = Math.Clamp(72.0 + (frac * 25.0), 72.0, 97.0);
                    var pct = (int)(frac * 100);
                    if (job.MediaType == OutputMediaType.Audio)
                    {
                        job.Phase = $"Converting audio to {job.Format} ({pct}%)...";
                    }
                    else
                    {
                        job.Phase = $"Processing media via FFmpeg ({pct}%)...";
                    }
                }
            };

            var swProc = System.Diagnostics.Stopwatch.StartNew();
            var procRes = await _mediaProcessor.ProcessMediaAsync(processReq, cancellationToken);
            swProc.Stop();
            _logger.LogInformation("Job {JobId}: [BREAKPOINT:8_FFmpegProcessing] Completed in {ElapsedMs}ms. Success={Success}",
                jobId, swProc.ElapsedMilliseconds, procRes.Success);
            if (!procRes.Success)
            {
                throw procRes.Exception ?? new MediaProcessingException(
                    procRes.ErrorMessage ?? "Media processing failed.",
                    "PROCESSING_FAILED",
                    "ffmpeg",
                    procRes.ExitCode,
                    "Processing",
                    jobId,
                    procRes.StandardError ?? "Processing failed",
                    "Media processing failed.",
                    isRetryable: false);
            }

            job.ProcessingStrategyUsed = procRes.ProcessingStrategyUsed;
            job.FallbackReason = procRes.FallbackReason;

            if (!File.Exists(tempProcessingPath))
            {
                throw new MediaProcessingException(
                    "Processed media file not found on disk after processing step.",
                    "PROCESSED_FILE_MISSING",
                    "ffmpeg",
                    -1,
                    "Processing",
                    jobId,
                    $"File does not exist: {tempProcessingPath}",
                    "Processed media file was not generated.",
                    isRetryable: false,
                    outputPath: tempProcessingPath);
            }

            // Phase 4: Finalization (validation and atomic move)
            var expectedDurationTs = TimeSpan.FromSeconds(job.ExpectedDurationSeconds ?? 0);
            await FinalizeOutputAsync(jobId, job, tempProcessingPath, targetPath, expectedDurationTs, cancellationToken, probe, swTotal);
            tempProcessingPath = null;
        }
        catch (OperationCanceledException)
        {
            job.Status = DownloadStatus.Cancelled;
            job.Phase = "Cancelled";
            _ = CleanupFailedDownloadAsync(tempProcessingPath ?? targetPath, job.Url);
        }
        catch (Exception ex)
        {
            await FailJobAsync(job, ex, tempProcessingPath, stagingDir);
        }
        finally
        {
            _jobCts.TryRemove(jobId, out _);

            try
            {
                var stagingSourceDir = Path.Combine(_stagingRoot, "source", jobId.ToString("N"));
                var stagingProcessedDir = Path.Combine(_stagingRoot, "processed", jobId.ToString("N"));
                if (Directory.Exists(stagingSourceDir)) Directory.Delete(stagingSourceDir, recursive: true);
                if (Directory.Exists(stagingProcessedDir)) Directory.Delete(stagingProcessedDir, recursive: true);
                if (!string.IsNullOrEmpty(stagingDir) && Directory.Exists(stagingDir)) Directory.Delete(stagingDir, recursive: true);
            }
            catch { }

            _semaphore.Release();
        }
    }

    private async Task FinalizeOutputAsync(
        Guid jobId,
        DownloadJobDto job,
        string tempProcessingPath,
        string targetPath,
        TimeSpan expectedDuration,
        CancellationToken cancellationToken,
        VideoProbeResultDto? probe = null,
        System.Diagnostics.Stopwatch? swTotal = null)
    {
        job.Status = DownloadStatus.Validating;
        job.Phase = "Validating final media output...";
        job.Progress = 90;

        var swVal = System.Diagnostics.Stopwatch.StartNew();
        var validationRes = await _mediaProcessor.ValidateOutputAsync(
            tempProcessingPath,
            job.MediaType,
            job.Format,
            expectedDuration,
            cancellationToken);
        swVal.Stop();
        _logger.LogInformation("Job {JobId}: [BREAKPOINT:9_Validation] Completed in {ElapsedMs}ms. IsValid={IsValid}",
            jobId, swVal.ElapsedMilliseconds, validationRes.IsValid);

        if (!validationRes.IsValid)
        {
            throw new MediaProcessingException(
                $"Final media output validation failed: {validationRes.ErrorMessage}",
                "OUTPUT_VALIDATION_FAILED",
                "ffprobe",
                -1,
                "Validating",
                jobId,
                validationRes.ErrorMessage ?? "Validation failed",
                $"Final media validation failed: {validationRes.ErrorMessage}",
                isRetryable: false,
                outputPath: tempProcessingPath);
        }

        // Atomic publishing: move temporary file into final library destination
        job.Status = DownloadStatus.Publishing;
        job.Phase = "Publishing to media library...";
        job.Progress = 95;

        var swMove = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            File.Move(tempProcessingPath, targetPath, overwrite: true);
        }
        catch (IOException)
        {
            File.Copy(tempProcessingPath, targetPath, overwrite: true);
            try { File.Delete(tempProcessingPath); } catch { }
        }
        swMove.Stop();
        _logger.LogInformation("Job {JobId}: [BREAKPOINT:10_AtomicMove] Published to '{Path}' in {ElapsedMs}ms",
            jobId, targetPath, swMove.ElapsedMilliseconds);

        if (!File.Exists(targetPath))
        {
            throw new MediaProcessingException(
                $"Published file not found at {targetPath} after move operation.",
                "PUBLISH_MOVE_FAILED",
                "filesystem",
                -1,
                "Publishing",
                jobId,
                $"Move failed from {tempProcessingPath} to {targetPath}",
                "Failed moving finalized media file to destination library.",
                isRetryable: false,
                outputPath: targetPath);
        }

        job.FilePath = targetPath;
        job.OutputFileSize = new FileInfo(targetPath).Length;

        string? harvestedTranscript = null;

        // If subtitles were requested and not audio, publish any downloaded subtitle files alongside the video and harvest transcript
        if (job.IncludeSubtitles && job.MediaType != OutputMediaType.Audio)
        {
            try
            {
                var stagingSourceDir = Path.Combine(_stagingRoot, "source", jobId.ToString("N"));
                if (Directory.Exists(stagingSourceDir))
                {
                    var subFiles = Directory.GetFiles(stagingSourceDir, $"source_{jobId:N}*.*")
                        .Where(f => f.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".srt", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (subFiles.Count > 0)
                    {
                        harvestedTranscript = SubtitleCleaner.ExtractPlainText(subFiles[0]);
                    }

                    var targetFolder = Path.GetDirectoryName(targetPath) ?? _mediaRoot;
                    var baseName = Path.GetFileNameWithoutExtension(targetPath);

                    foreach (var subFile in subFiles)
                    {
                        var ext = Path.GetExtension(subFile).ToLowerInvariant();
                        var fileNameNoExt = Path.GetFileNameWithoutExtension(subFile);
                        var langSuffix = "";
                        var dotIdx = fileNameNoExt.LastIndexOf('.');
                        if (dotIdx >= 0)
                        {
                            langSuffix = fileNameNoExt.Substring(dotIdx);
                        }

                        var destSubName = $"{baseName}{langSuffix}{ext}";
                        var destSubPath = Path.Combine(targetFolder, destSubName);
                        File.Copy(subFile, destSubPath, overwrite: true);
                        _logger.LogInformation("Job {JobId}: Published subtitle file to {DestSubPath}", jobId, destSubPath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Job {JobId}: Error publishing subtitle files", jobId);
            }
        }

        // Publish thumbnails & Jellyfin metadata (.nfo) for rich Jellyfin display
        try
        {
            var targetFolder = Path.GetDirectoryName(targetPath) ?? _mediaRoot;
            var baseName = Path.GetFileNameWithoutExtension(targetPath);

            var stagingSourceDir = Path.Combine(_stagingRoot, "source", jobId.ToString("N"));
            if (Directory.Exists(stagingSourceDir))
            {
                var thumbFiles = Directory.GetFiles(stagingSourceDir, $"source_{jobId:N}*.*")
                    .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || 
                                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) || 
                                f.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) || 
                                f.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (thumbFiles.Count > 0)
                {
                    var ext = Path.GetExtension(thumbFiles[0]).ToLowerInvariant();
                    var destThumbPath = Path.Combine(targetFolder, $"{baseName}-thumb{ext}");
                    var destPosterPath = Path.Combine(targetFolder, $"{baseName}{ext}");
                    File.Copy(thumbFiles[0], destThumbPath, overwrite: true);
                    File.Copy(thumbFiles[0], destPosterPath, overwrite: true);
                    _logger.LogInformation("Job {JobId}: Published thumbnail art to {DestThumbPath}", jobId, destThumbPath);
                }
            }

            // Write Jellyfin-compatible .nfo metadata file
            if (probe != null && !string.IsNullOrWhiteSpace(probe.Title))
            {
                var nfoPath = Path.Combine(targetFolder, $"{baseName}.nfo");
                var titleEscaped = EscapeXml(probe.Title ?? job.Title ?? baseName);
                var plotEscaped = EscapeXml(probe.Description ?? "");
                var studioEscaped = EscapeXml(probe.Uploader ?? "");
                var dateStr = DateTime.UtcNow.ToString("yyyy-MM-dd");
                var yearStr = DateTime.UtcNow.ToString("yyyy");

                var nfoContent = 
$@"<?xml version=""1.0"" encoding=""utf-8"" standalone=""yes""?>
<movie>
  <title>{titleEscaped}</title>
  <originaltitle>{titleEscaped}</originaltitle>
  <plot>{plotEscaped}</plot>
  <studio>{studioEscaped}</studio>
  <premiered>{dateStr}</premiered>
  <year>{yearStr}</year>
</movie>";
                File.WriteAllText(nfoPath, nfoContent);
                _logger.LogInformation("Job {JobId}: Published Jellyfin .nfo metadata to {NfoPath}", jobId, nfoPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Job {JobId}: Non-fatal error publishing thumbnail/NFO metadata", jobId);
        }

        var registeredVideoId = await RegisterFinalMediaAsync(
            targetPath, 
            job.Url, 
            job.CategoryName, 
            probe?.Description, 
            probe?.Uploader, 
            harvestedTranscript);

        job.Status = DownloadStatus.Completed;
        job.Phase = "Complete";
        job.Progress = 100;
        job.Speed = string.Empty;
        job.Eta = string.Empty;
        job.CompletedAt = DateTime.UtcNow;

        if (swTotal != null)
        {
            swTotal.Stop();
            _logger.LogInformation("Job {JobId}: [BREAKPOINT:TOTAL_SUCCESS] Download lifecycle finished in {TotalMs}ms ({TotalSec:F1}s). Published: '{TargetPath}'",
                jobId, swTotal.ElapsedMilliseconds, swTotal.ElapsedMilliseconds / 1000.0, targetPath);
        }
        else
        {
            _logger.LogInformation("Job {JobId} successfully completed and published to {TargetPath}", jobId, targetPath);
        }

        // Auto-organize via OCI SmartRoute if enabled in settings
        if (registeredVideoId.HasValue)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var organizer = scope.ServiceProvider.GetService<Application.MediaOrganizer.IMediaOrganizerService>();
                    if (organizer != null && organizer.GetSettings().AutoOrganizeOnDownloadComplete)
                    {
                        _logger.LogInformation("Job {JobId}: Auto-organizing media record {VideoId} using OCI SmartRoute...", jobId, registeredVideoId.Value);
                        await organizer.OrganizeMediaAsync(registeredVideoId.Value);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Job {JobId}: Auto-organize failed for video {VideoId}", jobId, registeredVideoId.Value);
                }
            });
        }

        // Trigger local library scan
        _ = Task.Run(async () =>
        {
            try
            {
                await _videoScanner.ScanAllAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning library after download of {TargetPath}", targetPath);
            }
        });

        // Notify Jellyfin server to refresh libraries instantly (Phase 5)
        _ = Task.Run(async () =>
        {
            try
            {
                var jellyfinUrl = _configuration["Jellyfin:BaseUrl"] ?? "http://jellyfin:8096";
                var jellyfinKey = _configuration["Jellyfin:ApiKey"];
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                if (!string.IsNullOrWhiteSpace(jellyfinKey))
                {
                    httpClient.DefaultRequestHeaders.Add("X-Emby-Token", jellyfinKey);
                }
                var res = await httpClient.PostAsync($"{jellyfinUrl.TrimEnd('/')}/Library/Refresh", null);
                if (res.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Job {JobId}: Triggered Jellyfin library refresh ({Code})", jobId, (int)res.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Jellyfin library refresh notification bypassed or offline: {Msg}", ex.Message);
            }
        });
    }

    private static string EscapeXml(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Replace("&", "&amp;")
                   .Replace("<", "&lt;")
                   .Replace(">", "&gt;")
                   .Replace("\"", "&quot;")
                   .Replace("'", "&apos;");
    }

    private async Task FailJobAsync(
        DownloadJobDto job,
        Exception ex,
        string? tempProcessingPath,
        string? stagingDir)
    {
        if (job.Status == DownloadStatus.Cancelled)
        {
            _logger.LogInformation("Job {JobId} was cancelled by user; skipping failure state override.", job.Id);
            if (!string.IsNullOrWhiteSpace(tempProcessingPath) && File.Exists(tempProcessingPath))
            {
                try { File.Delete(tempProcessingPath); } catch { }
            }
            await CleanupFailedDownloadAsync(null, job.Url);
            return;
        }

        job.CompletedAt = DateTime.UtcNow;
        job.WorkerId = Environment.MachineName;
        job.DiagnosticReference = $"ref_{job.Id:N}";

        if (ex is MediaProcessingException mpe)
        {
            job.Status = mpe.Stage switch
            {
                "Downloading" => DownloadStatus.DownloadFailed,
                "Probing" => DownloadStatus.ProbeFailed,
                "Processing" => DownloadStatus.ProcessingFailed,
                "Validating" => DownloadStatus.ValidationFailed,
                "Publishing" => DownloadStatus.PublishFailed,
                _ => DownloadStatus.Failed
            };
            job.ErrorCode = mpe.ErrorCode;
            job.ErrorMessage = mpe.UserMessage;
            _logger.LogError(
                "Job {JobId} FAILED in stage {Stage}.\n" +
                "  Tool: {Tool} (ExitCode: {ExitCode})\n" +
                "  ErrorCode: {ErrorCode}\n" +
                "  UserMessage: {UserMessage}\n" +
                "  Stderr: {Stderr}\n" +
                "  Args: {Args}\n" +
                "  InputPath: {InputPath}\n" +
                "  OutputPath: {OutputPath}\n" +
                "  Url: {Url}",
                job.Id, mpe.Stage, mpe.Tool, mpe.ExitCode, mpe.ErrorCode, mpe.UserMessage,
                mpe.Stderr, string.Join(" ", mpe.CommandArguments), mpe.InputPath, mpe.OutputPath,
                job.Url);
        }
        else if (ex is PathTooLongException ptle)
        {
            job.Status = DownloadStatus.PublishFailed;
            job.ErrorCode = "PATH_TOO_LONG";
            job.ErrorMessage = "The media file path or title is too long for the file system. Try using a shorter title or category name.";
            _logger.LogError(ptle, "Job {JobId} FAILED with PathTooLongException: {Message}", job.Id, ptle.Message);
        }
        else
        {
            job.Status = DownloadStatus.Failed;
            job.ErrorCode = "UNEXPECTED_ERROR";
            job.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Job {JobId} FAILED with unexpected exception.", job.Id);
        }

        if (!string.IsNullOrWhiteSpace(tempProcessingPath) && File.Exists(tempProcessingPath))
        {
            try { File.Delete(tempProcessingPath); } catch { }
        }

        await CleanupFailedDownloadAsync(null, job.Url);
    }

    private sealed record YtDlpDownloadRunResult(
        int ExitCode,
        string? DetectedDestination,
        string StandardOutput,
        string StandardError);

    private static string? TryParseDestinationPath(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        var m1 = Regex.Match(line, @"\[(?:download|ExtractAudio)\]\s+Destination:\s*(.+)$", RegexOptions.IgnoreCase);
        if (m1.Success) return m1.Groups[1].Value.Trim().Trim('"');

        var m2 = Regex.Match(line, @"\[Merger\]\s+Merging formats into\s*""?([^""]+)""?", RegexOptions.IgnoreCase);
        if (m2.Success) return m2.Groups[1].Value.Trim().Trim('"');

        var m3 = Regex.Match(line, @"\[download\]\s+(.+)\s+has already been downloaded", RegexOptions.IgnoreCase);
        if (m3.Success) return m3.Groups[1].Value.Trim().Trim('"');

        return null;
    }

    private async Task<YtDlpDownloadRunResult> RunYtDlpDownloadAsync(
        Guid jobId,
        DownloadJobDto job,
        IReadOnlyList<string> arguments,
        double segmentWeight,
        double baseProgress,
        CancellationToken cancellationToken)
    {
        string? detectedDestination = null;
        var lastBroadcastTime = DateTime.MinValue;
        var lastBroadcastProgress = -1.0;

        var swDl = System.Diagnostics.Stopwatch.StartNew();
        System.Diagnostics.Stopwatch? mergerSw = null;

        void OnProgressLine(string line)
        {
            var parsedPath = TryParseDestinationPath(line);
            if (!string.IsNullOrWhiteSpace(parsedPath))
            {
                detectedDestination = parsedPath;
            }

            if (line.Contains("[ExtractAudio]", StringComparison.OrdinalIgnoreCase))
            {
                job.Phase = "Extracting audio...";
                job.Progress = 72;
                job.Speed = string.Empty;
                job.Eta = string.Empty;
            }
            else if (line.Contains("[Merger]", StringComparison.OrdinalIgnoreCase))
            {
                job.Phase = "Merging video & audio...";
                job.Progress = 72;
                job.Speed = string.Empty;
                job.Eta = string.Empty;
                mergerSw = System.Diagnostics.Stopwatch.StartNew();
                _logger.LogInformation("Job {JobId}: [TIMING] yt-dlp reached [Merger] stage at {ElapsedMs}ms. Merging formats...",
                    jobId, swDl.ElapsedMilliseconds);
            }
            else
            {
                ParseDownloadProgress(job, line, segmentWeight, baseProgress);
            }

            if (_notifier != null && (DateTime.UtcNow - lastBroadcastTime > TimeSpan.FromMilliseconds(400) || Math.Abs(job.Progress - lastBroadcastProgress) >= 1.0))
            {
                lastBroadcastTime = DateTime.UtcNow;
                lastBroadcastProgress = job.Progress;
                _ = _notifier.NotifyJobProgressAsync(new DownloadJobProgressNotification(
                    job.Id,
                    Guid.Empty,
                    job.Title,
                    job.Status,
                    job.Phase,
                    job.Progress,
                    job.Speed,
                    job.Eta));
            }
        }

        var result = await _toolRunner.RunToolAsync(
            "yt-dlp",
            arguments,
            cancellationToken,
            timeout: TimeSpan.FromMinutes(45),
            onStandardOutputLine: OnProgressLine,
            onStandardErrorLine: OnProgressLine);

        swDl.Stop();
        if (mergerSw != null)
        {
            mergerSw.Stop();
            _logger.LogInformation("Job {JobId}: [TIMING] yt-dlp [Merger] stage completed in {MergerMs}ms.", jobId, mergerSw.ElapsedMilliseconds);
        }
        _logger.LogInformation("Job {JobId}: [TIMING] yt-dlp execution finished in {TotalMs}ms with ExitCode {Code}.", jobId, swDl.ElapsedMilliseconds, result.ExitCode);

        if (result.ExitCode != 0 && string.IsNullOrEmpty(job.ErrorMessage))
        {
            job.ErrorMessage = string.IsNullOrWhiteSpace(result.StandardError)
                ? "yt-dlp exited with an error code."
                : result.StandardError.Split('\n').FirstOrDefault(l => l.Contains("ERROR:", StringComparison.OrdinalIgnoreCase)) ?? result.StandardError;
        }

        return new YtDlpDownloadRunResult(result.ExitCode, detectedDestination, result.StandardOutput, result.StandardError);
    }

    private List<string> BuildYtDlpArguments(
        string url,
        string outputTemplate,
        string? quality,
        string? sectionArg,
        bool isAudio,
        bool includeSubtitles = false,
        string? subtitleLanguage = null,
        bool includeProxy = true,
        OutputFormat? targetFormat = null,
        string? overrideExtractorArgs = null,
        bool includeUserAgent = true,
        bool clearCache = false,
        string? audioLanguage = null,
        bool useAria2c = true,
        VideoProbeResultDto? probe = null,
        bool writeAutoSubs = true)
    {
        var args = new List<string>
        {
            "--newline",
            "--no-colors",
            "--no-playlist",
            "--no-check-certificates",
            "--no-warnings",
            "--abort-on-unavailable-fragments",
            "--force-overwrites",
            "--buffer-size", "1024K",
            "--retries", "10",
            "--fragment-retries", "10"
        };

        if (useAria2c)
        {
            args.Add("--downloader");
            args.Add("aria2c");
            args.Add("--downloader-args");
            args.Add("aria2c:-x 16 -s 16 -k 1M --file-allocation=none --disable-ipv6=true");
        }

        args.AddRange(new[]
        {
            "--ffmpeg-location", _toolRunner.GetFfmpegPath(),
            "--postprocessor-args", "ffmpeg:-nostdin -threads 0",
            "--js-runtimes", "node",
            "--compat-options", "no-live-chat"
        });

        if (clearCache)
        {
            args.Add("--rm-cache-dir");
        }

        if (url.Contains("facebook.com", StringComparison.OrdinalIgnoreCase) || url.Contains("fb.watch", StringComparison.OrdinalIgnoreCase))
        {
            args.Add("--referer");
            args.Add("https://www.facebook.com/");
        }

        if (!string.IsNullOrEmpty(sectionArg))
        {
            args.Add("--download-sections");
            args.Add(sectionArg);
        }

        if (isAudio)
        {
            includeSubtitles = false;
        }

        var hasMultiAudio = !string.IsNullOrWhiteSpace(audioLanguage) &&
                            !audioLanguage.Equals("all", StringComparison.OrdinalIgnoreCase) &&
                            audioLanguage.Contains(',');

        if (hasMultiAudio)
        {
            args.Add("--audio-multistreams");
        }

        if (isAudio)
        {
            if (!string.IsNullOrWhiteSpace(audioLanguage) && !audioLanguage.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                if (audioLanguage.Contains(','))
                {
                    var langs = audioLanguage.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Take(2)
                        .ToList();
                    if (langs.Count > 1)
                    {
                        var audioParts = string.Join("+", langs.Select(l => $"bestaudio[language={l}]"));
                        args.Add("-f");
                        args.Add($"{audioParts}/bestaudio/best");
                    }
                    else if (langs.Count == 1)
                    {
                        args.Add("-f");
                        args.Add($"bestaudio[language={langs[0]}]/bestaudio[language^={langs[0]}]/bestaudio/best");
                    }
                }
                else
                {
                    args.Add("-f");
                    args.Add($"bestaudio[language={audioLanguage}]/bestaudio[language^={audioLanguage}]/bestaudio/best");
                }
            }
            else
            {
                args.Add("-f");
                args.Add("bestaudio/best");
            }
        }
        else
        {
            var formatSelector = BuildFormatSelector(quality, targetFormat, audioLanguage);
            args.Add("-f");
            args.Add(formatSelector);
            args.Add("--merge-output-format");
            args.Add("mkv/mp4");
            if (!string.IsNullOrWhiteSpace(audioLanguage) && audioLanguage.Contains(','))
            {
                args.Add("--audio-multistreams");
            }
            args.Add("--write-thumbnail");

            if (includeSubtitles)
            {
                args.Add("--write-subs");
                if (writeAutoSubs)
                {
                    args.Add("--write-auto-subs");
                }
                args.Add("--sub-langs");
                args.Add(NormalizeSubtitleLanguage(subtitleLanguage, probe));
                args.Add("--sub-format");
                args.Add("vtt/best");
                args.Add("--embed-subs");
                args.Add("--convert-subs");
                args.Add("vtt");
            }
        }

        args.Add("-o");
        args.Add(outputTemplate);

        Media.YtDlpBypassHelper.ApplyRestrictionBypassArgs(
            args, 
            _settingsStore?.GetSettings(), 
            _configuration, 
            _mediaRoot, 
            includeProxy,
            overrideExtractorArgs,
            includeUserAgent);

        args.Add(url);

        return args;
    }

    public static string NormalizeSubtitleLanguage(string? lang, VideoProbeResultDto? probe = null)
    {
        const string SafeAutoSubs = ".*-orig,en-orig,en,en-US,en-GB,es-orig,es,es-419,fr-orig,fr,de-orig,de,hi-orig,hi,ja-orig,ja,zh-Hans,zh-Hant";

        if (string.IsNullOrWhiteSpace(lang) || lang.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            if (probe?.SubtitleTracks != null && probe.SubtitleTracks.Count > 0)
            {
                var origTrack = probe.SubtitleTracks.FirstOrDefault(t => t.IsOriginal || t.Code.EndsWith("-orig", StringComparison.OrdinalIgnoreCase));
                if (origTrack != null && !string.IsNullOrWhiteSpace(origTrack.Code))
                {
                    var cleanCode = origTrack.Code.Replace("-orig", "");
                    return $"{cleanCode}-orig,{cleanCode}";
                }
                var firstTrack = probe.SubtitleTracks.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.Code));
                if (firstTrack != null)
                {
                    var cleanCode = firstTrack.Code.Replace("-orig", "");
                    return $"{cleanCode}-orig,{cleanCode}";
                }
            }

            return SafeAutoSubs;
        }

        var trimmed = lang.Trim();

        // "all" available subtitles disabled to prevent YouTube 429 rate limits; fallback to at most 2 safe tracks
        if (trimmed.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            if (probe?.SubtitleTracks != null && probe.SubtitleTracks.Count > 0)
            {
                var topTwo = probe.SubtitleTracks
                    .Where(t => !string.IsNullOrWhiteSpace(t.Code) && !t.Code.Contains("-to-", StringComparison.OrdinalIgnoreCase))
                    .Take(2)
                    .Select(t => NormalizeSingleSubtitleLang(t.Code))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (topTwo.Count > 0)
                {
                    return string.Join(",", topTwo);
                }
            }

            return "en-orig,en";
        }

        // Support up to 2 comma-separated subtitle languages
        if (trimmed.Contains(','))
        {
            var parts = trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(2)
                .Select(NormalizeSingleSubtitleLang)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return string.Join(",", parts);
        }

        return NormalizeSingleSubtitleLang(trimmed);
    }

    private static string NormalizeSingleSubtitleLang(string trimmed)
    {
        if (trimmed.Equals("en.*,en", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("en.*", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("en", StringComparison.OrdinalIgnoreCase))
        {
            return "en-orig,en,en-US,en-GB";
        }

        if (trimmed.EndsWith("-orig", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        if (trimmed.Equals("es", StringComparison.OrdinalIgnoreCase))
        {
            return "es-orig,es,es-419,es-ES";
        }

        if (trimmed.Equals("pt", StringComparison.OrdinalIgnoreCase))
        {
            return "pt-orig,pt,pt-BR,pt-PT";
        }

        if (trimmed.Equals("zh", StringComparison.OrdinalIgnoreCase))
        {
            return "zh-orig,zh,zh-Hans,zh-Hant";
        }

        return $"{trimmed}-orig,{trimmed}";
    }

    public static string BuildFormatSelector(string? quality, OutputFormat? targetFormat = null, string? audioLanguage = null)
    {
        var isMp4 = targetFormat == null || targetFormat == OutputFormat.Mp4;
        var hasMultiAudio = !string.IsNullOrWhiteSpace(audioLanguage) &&
                            !audioLanguage.Equals("all", StringComparison.OrdinalIgnoreCase) &&
                            audioLanguage.Contains(',');

        if (!string.IsNullOrWhiteSpace(audioLanguage))
        {
            string videoPart;
            int h = 0;
            var m = !string.IsNullOrWhiteSpace(quality) ? Regex.Match(quality, @"^(\d+)p", RegexOptions.IgnoreCase) : null;
            if (m != null && m.Success && int.TryParse(m.Groups[1].Value, out h))
            {
                videoPart = (isMp4 && h <= 1080 && !hasMultiAudio)
                    ? $"bestvideo[height<={h}][ext=mp4]"
                    : $"bestvideo[height<={h}]";
            }
            else
            {
                videoPart = (isMp4 && !hasMultiAudio) ? "bestvideo[ext=mp4]" : "bestvideo";
            }

            // "all" disabled: fallback to default best audio
            if (audioLanguage.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                return $"{videoPart}+bestaudio/best";
            }

            if (audioLanguage.Contains(','))
            {
                var langs = audioLanguage.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Take(2)
                    .ToList();
                if (langs.Count > 1)
                {
                    var audioParts = string.Join("+", langs.Select(l => $"(bestaudio[language={l}]/bestaudio[language^={l}])"));
                    return $"{videoPart}+{audioParts}/{videoPart}+bestaudio/best";
                }
                else if (langs.Count == 1)
                {
                    return $"{videoPart}+bestaudio[language={langs[0]}]/{videoPart}+bestaudio[language^={langs[0]}]/{videoPart}+bestaudio/best";
                }
            }

            return $"{videoPart}+bestaudio[language={audioLanguage}]/{videoPart}+bestaudio[language^={audioLanguage}]/{videoPart}+bestaudio/best";
        }

        if (string.IsNullOrWhiteSpace(quality) || quality.Contains("Default", StringComparison.OrdinalIgnoreCase))
        {
            return isMp4
                ? "bestvideo[ext=mp4]+bestaudio[ext=m4a]/bestvideo+bestaudio/best"
                : "bestvideo+bestaudio/best";
        }

        var match = Regex.Match(quality, @"^(\d+)p", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var height))
        {
            if (isMp4 && height <= 1080)
            {
                return $"bestvideo[height<={height}][ext=mp4]+bestaudio[ext=m4a]/bestvideo[height<={height}]+bestaudio/best[height<={height}]/best";
            }

            return $"bestvideo[height<={height}]+bestaudio/best[height<={height}]/best";
        }

        return isMp4
            ? "bestvideo[ext=mp4]+bestaudio[ext=m4a]/bestvideo+bestaudio/best"
            : "bestvideo+bestaudio/best";
    }

    private async Task<(string? FilePath, string? ErrorMessage)> GetTargetFilePathAsync(
        string url, 
        string outputTemplate, 
        CancellationToken token)
    {
        var settings = _settingsStore?.GetSettings();
        var hasProxy = !string.IsNullOrWhiteSpace(settings?.Proxy) || !string.IsNullOrWhiteSpace(_configuration?["Media:Proxy"]);

        var args = new List<string>
        {
            "--print", "filename",
            "--no-playlist",
            "--no-check-certificates",
            "--no-warnings",
            "-o", outputTemplate
        };

        Media.YtDlpBypassHelper.ApplyRestrictionBypassArgs(args, settings, _configuration, _mediaRoot, includeProxy: true);
        args.Add(url);

        var result = await _toolRunner.RunToolAsync("yt-dlp", args, token, timeout: TimeSpan.FromSeconds(30));
        if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            return (result.StandardOutput.Trim().Split('\n').LastOrDefault()?.Trim(), null);
        }

        // If proxy probe failed or timed out, retry directly without proxy
        if (hasProxy)
        {
            _logger.LogWarning("yt-dlp filename probe failed with proxy (ExitCode {Code}). Retrying directly without proxy for {Url}. Stderr: {Err}",
                result.ExitCode, url, result.StandardError);

            var directArgs = new List<string>
            {
                "--print", "filename",
                "--no-playlist",
                "--no-check-certificates",
                "--no-warnings",
                "-o", outputTemplate
            };

            Media.YtDlpBypassHelper.ApplyRestrictionBypassArgs(directArgs, settings, _configuration, _mediaRoot, includeProxy: false);
            directArgs.Add(url);

            var directResult = await _toolRunner.RunToolAsync("yt-dlp", directArgs, token, timeout: TimeSpan.FromSeconds(30));
            if (directResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(directResult.StandardOutput))
            {
                _logger.LogInformation("Filename resolution succeeded on direct retry without proxy for {Url}", url);
                return (directResult.StandardOutput.Trim().Split('\n').LastOrDefault()?.Trim(), null);
            }

            result = directResult;
        }

        var cleanError = ExtractMeaningfulYtDlpError(result.StandardError);
        return (null, cleanError);
    }

    private static string ExtractMeaningfulYtDlpError(string? stderr, string? stdout = null)
    {
        var text = string.IsNullOrWhiteSpace(stderr) ? (stdout ?? string.Empty) : stderr;
        if (string.IsNullOrWhiteSpace(text)) return "yt-dlp failed to process URL";

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var botCheck = lines.FirstOrDefault(l => l.Contains("Sign in to confirm", StringComparison.OrdinalIgnoreCase));
        if (botCheck != null)
        {
            return "YouTube requested bot verification: Sign in to confirm you're not a bot. Please remove VPN/proxy or configure cookies.txt in Settings.";
        }

        var aria2cError = lines.FirstOrDefault(l => l.Contains("errorCode=1", StringComparison.OrdinalIgnoreCase) || l.Contains("aria2c exited with code 1", StringComparison.OrdinalIgnoreCase));
        if (aria2cError != null)
        {
            return "Downloader network error occurred while fetching media stream.";
        }

        var errorLine = lines.FirstOrDefault(l => l.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase));
        if (errorLine != null)
        {
            var msg = errorLine.Substring("ERROR:".Length).Trim();
            var bracketIdx = msg.IndexOf(']');
            if (bracketIdx >= 0 && bracketIdx < msg.Length - 1)
            {
                msg = msg.Substring(bracketIdx + 1).Trim();
            }
            return msg;
        }

        return lines.LastOrDefault() ?? "yt-dlp exited with an error";
    }

    private static bool IsAria2cFailure(string? stderr, string? stdout, string? errorMessage)
    {
        var combined = $"{stderr} {stdout} {errorMessage}";
        return combined.Contains("errorCode=1", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("unreachable network", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("aria2c exited with code 1", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("aria2c: error", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("A socket operation was attempted to an unreachable network", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSubtitleOr429Failure(string? stderr, string? stdout, string? errorMessage)
    {
        var combined = $"{stderr} {stdout} {errorMessage}";
        return combined.Contains("429", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("Unable to download video subtitles", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("timedtext", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("subtitles for", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsForbiddenOrBotBlocked(string? stderr, string? errorMessage)
    {
        var combined = $"{stderr} {errorMessage}";
        return combined.Contains("403", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("Forbidden", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("Sign in to confirm", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("unable to download video data", StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeFileName(string? name)
    {
        return SafePathHelper.SanitizeFileName(name);
    }

    private static void ParseDownloadProgress(
        DownloadJobDto job,
        string line,
        double segmentWeight,
        double baseProgress)
    {
        if (string.IsNullOrWhiteSpace(line)) return;

        var percentMatch = PercentRegex.Match(line);
        if (percentMatch.Success && double.TryParse(percentMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var progressVal))
        {
            var calculated = baseProgress + (progressVal * segmentWeight);
            job.Progress = Math.Clamp(calculated, 0.0, 99.0);
        }

        var speedMatch = SpeedRegex.Match(line);
        if (speedMatch.Success)
        {
            job.Speed = speedMatch.Groups[1].Value.Trim();
        }

        var etaMatch = EtaRegex.Match(line);
        if (etaMatch.Success)
        {
            var etaVal = etaMatch.Groups[1].Value.Trim();
            if (!string.Equals(etaVal, "Unknown", StringComparison.OrdinalIgnoreCase))
            {
                job.Eta = etaVal;
            }
        }
    }

    private void CleanupStagingAndProcessingArtifacts()
    {
        try
        {
            // Clean old staging directories
            if (Directory.Exists(_stagingRoot))
            {
                foreach (var dir in Directory.GetDirectories(_stagingRoot, "stage_*"))
                {
                    try { Directory.Delete(dir, recursive: true); } catch { }
                }
            }

            // Clean abandoned .processing files in media root
            if (Directory.Exists(_mediaRoot))
            {
                foreach (var file in Directory.GetFiles(_mediaRoot, "*.processing.*", SearchOption.AllDirectories))
                {
                    try { File.Delete(file); } catch { }
                }
            }
        }
        catch { }
    }

    private static string SanitizeCategoryName(string? rawCategory)
    {
        return SafePathHelper.SanitizeCategoryName(rawCategory);
    }

    private static string ValidateAndContainPath(string root, string targetPath)
    {
        var fullRoot = Path.GetFullPath(root);
        var fullTarget = Path.GetFullPath(targetPath);
        if (!fullTarget.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Path traversal detected: {targetPath} is outside {root}");
        }
        return fullTarget;
    }

    private static string EnsureUniqueFilePath(string filePath)
    {
        return SafePathHelper.EnsureUniqueFilePath(filePath);
    }

    private static void EnsureSufficientDiskSpace(string mediaRoot, string stagingRoot, long minimumFreeBytes)
    {
        CheckPathSpace(mediaRoot, minimumFreeBytes);
        CheckPathSpace(stagingRoot, minimumFreeBytes);
    }

    private static void CheckPathSpace(string path, long minimumFreeBytes)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(root)) return;
            var drive = new DriveInfo(root);
            if (drive.AvailableFreeSpace < minimumFreeBytes)
            {
                throw new IOException($"Insufficient disk space on drive '{root}'. Available: {drive.AvailableFreeSpace / (1024 * 1024)} MB, required: {minimumFreeBytes / (1024 * 1024)} MB.");
            }
        }
        catch (Exception ex) when (ex is not IOException)
        {
            // Ignore non-IO errors on special filesystems
        }
    }

    private async Task<Guid?> RegisterFinalMediaAsync(
        string filePath, 
        string sourceUrl, 
        string categoryName,
        string? description = null,
        string? uploader = null,
        string? transcript = null)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<VideoDbContext>();

            var category = await dbContext.Categories.FirstOrDefaultAsync(c => c.Name.ToLower() == categoryName.ToLower());
            if (category == null)
            {
                category = new Category
                {
                    Name = categoryName,
                    CreatedAt = DateTime.UtcNow
                };
                dbContext.Categories.Add(category);
                try { await dbContext.SaveChangesAsync(); } catch { }
            }

            var existing = await dbContext.Videos.FirstOrDefaultAsync(v => v.FilePath == filePath);
            if (existing == null)
            {
                var newVideo = new Video
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    DisplayTitle = Path.GetFileNameWithoutExtension(filePath),
                    CategoryId = category.Id,
                    ScanStatus = ScanState.Ready,
                    SourceUrl = sourceUrl,
                    RawDescription = description,
                    Uploader = uploader,
                    Transcript = transcript,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                dbContext.Videos.Add(newVideo);
                await dbContext.SaveChangesAsync();
                return newVideo.Id;
            }
            else
            {
                existing.SourceUrl = sourceUrl;
                existing.ScanStatus = ScanState.Ready;
                if (!string.IsNullOrWhiteSpace(description)) existing.RawDescription = description;
                if (!string.IsNullOrWhiteSpace(uploader)) existing.Uploader = uploader;
                if (!string.IsNullOrWhiteSpace(transcript)) existing.Transcript = transcript;
                existing.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
                return existing.Id;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed registering final media record for {FilePath}", filePath);
            return null;
        }
    }

    private async Task CleanupFailedDownloadAsync(string? targetPath, string sourceUrl)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<VideoDbContext>();

            var video = await dbContext.Videos.FirstOrDefaultAsync(v => v.SourceUrl == sourceUrl && v.ScanStatus != ScanState.Ready);
            if (video != null)
            {
                dbContext.Videos.Remove(video);
                await dbContext.SaveChangesAsync();
            }

            if (!string.IsNullOrWhiteSpace(targetPath) && File.Exists(targetPath))
            {
                try { File.Delete(targetPath); } catch { }
            }
        }
        catch { }
    }

    public static bool IsAudioQuality(string? quality)
    {
        if (string.IsNullOrWhiteSpace(quality)) return false;
        var q = quality.Trim().ToLowerInvariant();
        return q is "audio" or "audio_only" or "audio only" or "mp3" || q.StartsWith("audio only");
    }

    public static void ParseProgressForTesting(DownloadJobDto job, string line, double segmentWeight = 1.0, double baseProgress = 0.0) =>
        ParseDownloadProgress(job, line, segmentWeight, baseProgress);
}
