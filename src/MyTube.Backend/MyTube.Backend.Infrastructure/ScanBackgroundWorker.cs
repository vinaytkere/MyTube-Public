using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application;

namespace MyTube.Backend.Infrastructure;

public class ScanBackgroundWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScanBackgroundWorker> _logger;
    private readonly TimeSpan _scanInterval = TimeSpan.FromHours(1); // Run periodic reconciliation scan every 1 hour

    public ScanBackgroundWorker(IServiceProvider serviceProvider, ILogger<ScanBackgroundWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scan Background Worker starting...");

        // 1. Initial scan on startup (wait a few seconds to let the web app spin up completely)
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            _logger.LogInformation("Triggering initial startup scan...");
            
            using var scope = _serviceProvider.CreateScope();
            var scanner = scope.ServiceProvider.GetRequiredService<IVideoScanner>();
            await scanner.ScanAllAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error occurred during initial startup video scan.");
        }

        // 2. Periodic reconciliation scans
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_scanInterval, stoppingToken);
                
                _logger.LogInformation("Triggering periodic reconciliation video scan...");
                using var scope = _serviceProvider.CreateScope();
                var scanner = scope.ServiceProvider.GetRequiredService<IVideoScanner>();
                await scanner.ScanAllAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error occurred during periodic reconciliation video scan.");
            }
        }

        _logger.LogInformation("Scan Background Worker stopping.");
    }
}
