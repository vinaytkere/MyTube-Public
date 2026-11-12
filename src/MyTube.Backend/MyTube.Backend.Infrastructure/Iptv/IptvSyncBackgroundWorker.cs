using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application.Iptv;

namespace MyTube.Backend.Infrastructure.Iptv;

public class IptvSyncBackgroundWorker : BackgroundService
{
    private readonly IIptvSyncService _syncService;
    private readonly ILogger<IptvSyncBackgroundWorker> _logger;

    public IptvSyncBackgroundWorker(
        IIptvSyncService syncService,
        ILogger<IptvSyncBackgroundWorker> logger)
    {
        _syncService = syncService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IPTV Sync Background Worker started.");

        // Wait 15 seconds after startup before the initial sync check
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("IPTV background worker running periodic catalog check...");
                await _syncService.SyncCatalogAsync(force: false, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in periodic IPTV sync worker: {Message}", ex.Message);
            }

            try
            {
                // Run sync check every 24 hours
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("IPTV Sync Background Worker stopped.");
    }
}

