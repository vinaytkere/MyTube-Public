using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application;

namespace MyTube.Backend.Infrastructure;

public class MediaLibraryWatcherService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MediaLibraryWatcherService> _logger;
    private readonly string _mediaRoot;
    private FileSystemWatcher? _watcher;

    private readonly ConcurrentDictionary<string, Timer> _debouncedFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly string[] _supportedExtensions = [".mp4", ".mkv", ".avi", ".mov", ".webm", ".flv", ".m4v", ".3gp", ".mp3", ".m4a"];
    private readonly TimeSpan _debounceDelay = TimeSpan.FromSeconds(6);

    public bool IsWatching => _watcher != null && _watcher.EnableRaisingEvents;

    public MediaLibraryWatcherService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<MediaLibraryWatcherService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
        _mediaRoot = configuration["Media:RootPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "media");
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (!Directory.Exists(_mediaRoot))
            {
                Directory.CreateDirectory(_mediaRoot);
            }

            _watcher = new FileSystemWatcher(_mediaRoot)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.DirectoryName
            };

            _watcher.Created += OnFileSystemEvent;
            _watcher.Changed += OnFileSystemEvent;
            _watcher.Renamed += OnRenamedEvent;
            _watcher.Error += OnWatcherError;

            _watcher.EnableRaisingEvents = true;
            _logger.LogInformation($"MediaLibraryWatcherService actively monitoring '{_mediaRoot}' for new media drops.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to initialize FileSystemWatcher on '{_mediaRoot}'");
        }

        return Task.CompletedTask;
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        HandleFileCandidate(e.FullPath);
    }

    private void OnRenamedEvent(object sender, RenamedEventArgs e)
    {
        HandleFileCandidate(e.FullPath);
    }

    private void HandleFileCandidate(string fullPath)
    {
        try
        {
            // Ignore system folders and recycle bin
            if (fullPath.Contains(".recycle", StringComparison.OrdinalIgnoreCase) ||
                fullPath.Contains(".tmp", StringComparison.OrdinalIgnoreCase) ||
                fullPath.EndsWith(".part", StringComparison.OrdinalIgnoreCase) ||
                fullPath.EndsWith(".crdownload", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var ext = Path.GetExtension(fullPath)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !_supportedExtensions.Contains(ext))
            {
                return;
            }

            // Debounce: reset or create timer for this file
            _debouncedFiles.AddOrUpdate(fullPath,
                path => new Timer(OnFileReady, path, _debounceDelay, Timeout.InfiniteTimeSpan),
                (path, existingTimer) =>
                {
                    existingTimer.Change(_debounceDelay, Timeout.InfiniteTimeSpan);
                    return existingTimer;
                });
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, $"Error handling watcher event for {fullPath}");
        }
    }

    private void OnFileReady(object? state)
    {
        if (state is not string filePath) return;

        // Clean up timer
        if (_debouncedFiles.TryRemove(filePath, out var timer))
        {
            timer.Dispose();
        }

        try
        {
            if (!File.Exists(filePath)) return;

            // Check if file is unlocked / finished writing
            if (!IsFileReady(filePath))
            {
                _logger.LogDebug($"File '{filePath}' is still locked by another process. Re-queuing watcher debounce.");
                _debouncedFiles.AddOrUpdate(filePath,
                    path => new Timer(OnFileReady, path, _debounceDelay, Timeout.InfiniteTimeSpan),
                    (path, existingTimer) =>
                    {
                        existingTimer.Change(_debounceDelay, Timeout.InfiniteTimeSpan);
                        return existingTimer;
                    });
                return;
            }

            _logger.LogInformation($"MediaLibraryWatcher detected new/modified ready file: '{filePath}'. Triggering library scanner sync.");
            
            using var scope = _serviceProvider.CreateScope();
            var scanner = scope.ServiceProvider.GetRequiredService<IVideoScanner>();
            _ = Task.Run(() => scanner.ScanAllAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing ready file from watcher: {filePath}");
        }
    }

    private static bool IsFileReady(string filename)
    {
        try
        {
            using var stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return stream.Length > 0;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        _logger.LogWarning(e.GetException(), "FileSystemWatcher encountered an error.");
    }

    public override void Dispose()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }

        foreach (var t in _debouncedFiles.Values)
        {
            t.Dispose();
        }
        _debouncedFiles.Clear();

        base.Dispose();
    }
}
