using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application;
using MyTube.Backend.Domain;

namespace MyTube.Backend.Infrastructure;

public class DownloadBackgroundWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDownloadQueue _queue;
    private readonly IDownloadManager _downloadManager;
    private readonly ILogger<DownloadBackgroundWorker> _logger;
    private readonly SemaphoreSlim _concurrencyLimiter = new(2, 2); // Limit to 2 concurrent downloads

    public DownloadBackgroundWorker(
        IServiceProvider serviceProvider,
        IDownloadQueue queue,
        IDownloadManager downloadManager,
        ILogger<DownloadBackgroundWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _queue = queue;
        _downloadManager = downloadManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Download Background Worker starting...");

        // 1. Startup recovery: query database for any jobs that were queued or in progress during last shutdown
        await RecoverPendingJobsAsync(stoppingToken);

        // 2. Consume from channel with concurrency limit
        try
        {
            await foreach (var jobId in _queue.ReadAllAsync(stoppingToken))
            {
                if (_downloadManager.IsJobCancelled(jobId))
                {
                    _logger.LogInformation("Job {JobId} was cancelled; skipping without acquiring concurrency slot.", jobId);
                    continue;
                }

                await _concurrencyLimiter.WaitAsync(stoppingToken);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _serviceProvider.CreateScope();
                        await ((DownloadManager)_downloadManager).ExecuteQueuedJobAsync(jobId, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Job {JobId} cancelled due to worker shutdown.", jobId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unhandled error processing queued download job {JobId}", jobId);
                    }
                    finally
                    {
                        _concurrencyLimiter.Release();
                    }
                }, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Download Background Worker execution was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in Download Background Worker execution loop.");
        }

        _logger.LogInformation("Download Background Worker stopped.");
    }

    private async Task RecoverPendingJobsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VideoDbContext>();

            var pendingJobs = await db.DownloadJobs
                .Where(j => j.Status == DownloadStatus.Queued || j.Status == DownloadStatus.Downloading || j.Status == DownloadStatus.Probing)
                .OrderBy(j => j.CreatedAt)
                .ToListAsync(cancellationToken);

            if (pendingJobs.Count > 0)
            {
                _logger.LogInformation("Found {Count} pending/interrupted download jobs in database to recover.", pendingJobs.Count);

                foreach (var job in pendingJobs)
                {
                    if (job.Status != DownloadStatus.Queued)
                    {
                        job.Status = DownloadStatus.Queued;
                        job.Phase = "Recovered after server restart";
                    }

                    await _queue.EnqueueAsync(job.Id, cancellationToken);
                }

                await db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Successfully re-enqueued {Count} recovered download jobs.", pendingJobs.Count);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to recover pending download jobs from database on startup.");
        }
    }
}
