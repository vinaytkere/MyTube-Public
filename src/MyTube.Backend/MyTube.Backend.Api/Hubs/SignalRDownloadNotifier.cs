using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application;

namespace MyTube.Backend.Api.Hubs;

public class SignalRDownloadNotifier : IDownloadNotifier
{
    private readonly IHubContext<DownloadHub> _hubContext;
    private readonly ILogger<SignalRDownloadNotifier> _logger;

    public SignalRDownloadNotifier(IHubContext<DownloadHub> hubContext, ILogger<SignalRDownloadNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyJobProgressAsync(DownloadJobProgressNotification notification, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("JobProgress", notification, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast JobProgress for job {JobId}", notification.JobId);
        }
    }

    public async Task NotifyJobCompletedAsync(DownloadJobCompletedNotification notification, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("JobCompleted", notification, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast JobCompleted for job {JobId}", notification.JobId);
        }
    }

    public async Task NotifyJobFailedAsync(DownloadJobFailedNotification notification, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("JobFailed", notification, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast JobFailed for job {JobId}", notification.JobId);
        }
    }

    public async Task NotifyBatchUpdatedAsync(DownloadBatchUpdatedNotification notification, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("BatchUpdated", notification, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast BatchUpdated for batch {BatchId}", notification.BatchId);
        }
    }
}
