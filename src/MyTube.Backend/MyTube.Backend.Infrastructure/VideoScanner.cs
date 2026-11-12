using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application;
using MyTube.Backend.Domain;

namespace MyTube.Backend.Infrastructure;

public class VideoScanner : IVideoScanner
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMediaProcessor _mediaProcessor;
    private readonly ILogger<VideoScanner> _logger;
    private readonly string _mediaRoot;
    private readonly string _cacheRoot;
    private readonly string[] _supportedExtensions = [".mp4", ".mkv", ".avi", ".mov", ".webm", ".flv", ".m4v", ".3gp", ".mp3", ".m4a"];

    private int _isScanning; // 0 = false, 1 = true
    private string _statusMessage = "Idle";

    public bool IsScanning => _isScanning == 1;
    public string ScanStatusMessage => _statusMessage;

    public VideoScanner(
        IServiceScopeFactory scopeFactory,
        IMediaProcessor mediaProcessor,
        IConfiguration configuration,
        ILogger<VideoScanner> logger)
    {
        _scopeFactory = scopeFactory;
        _mediaProcessor = mediaProcessor;
        _logger = logger;

        _mediaRoot = configuration["Media:RootPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "media");
        _cacheRoot = configuration["Media:CachePath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "cache");

        // Ensure directories exist
        if (!Directory.Exists(_mediaRoot))
        {
            try
            {
                Directory.CreateDirectory(_mediaRoot);
                _logger.LogInformation($"Created media root directory at {_mediaRoot}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Could not create media root directory at {_mediaRoot}: {ex.Message}");
            }
        }

        var thumbnailDir = Path.Combine(_cacheRoot, "thumbnails");
        if (!Directory.Exists(thumbnailDir))
        {
            try
            {
                Directory.CreateDirectory(thumbnailDir);
                _logger.LogInformation($"Created cache thumbnails directory at {thumbnailDir}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Could not create cache thumbnails directory: {ex.Message}");
            }
        }
    }

    public async Task ScanAllAsync()
    {
        // Prevent concurrent scans
        if (Interlocked.CompareExchange(ref _isScanning, 1, 0) == 1)
        {
            _logger.LogWarning("Scan already in progress. Skipping scan request.");
            return;
        }

        _logger.LogInformation("Starting complete video scan...");
        _statusMessage = "Scanning files on disk...";

        try
        {
            if (!Directory.Exists(_mediaRoot))
            {
                _statusMessage = $"Scan failed: Media root path {_mediaRoot} is not mounted or does not exist.";
                _logger.LogError(_statusMessage);
                return;
            }

            // Find all video files on disk
            var filesOnDisk = Directory.EnumerateFiles(_mediaRoot, "*.*", SearchOption.AllDirectories)
                .Where(file => _supportedExtensions.Contains(Path.GetExtension(file).ToLower()))
                .ToList();

            _logger.LogInformation($"Found {filesOnDisk.Count} video files on disk.");
            _statusMessage = $"Syncing database metadata for {filesOnDisk.Count} files...";

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<VideoDbContext>();

            // Get all existing videos in db
            var videosInDb = await dbContext.Videos.ToListAsync();
            var dbVideoMap = videosInDb.GroupBy(v => v.FilePath).ToDictionary(g => g.Key, g => g.First());

            var discoveredFiles = new ConcurrentBag<string>();
            var missingVideos = videosInDb.Where(v => !filesOnDisk.Contains(v.FilePath)).ToList();

            // Mark missing files
            if (missingVideos.Any())
            {
                _logger.LogInformation($"Found {missingVideos.Count} missing videos. Updating status to Missing.");
                foreach (var video in missingVideos)
                {
                    video.ScanStatus = ScanState.Missing;
                }
                await dbContext.SaveChangesAsync();
            }

            // Preload existing categories to eliminate N+1 queries during disk scan
            var categoryCache = await dbContext.Categories
                .ToDictionaryAsync(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);

            // Ensure default Downloads category exists
            if (!categoryCache.TryGetValue("Downloads", out var defaultCategory))
            {
                defaultCategory = await dbContext.Categories.FirstOrDefaultAsync(c => c.Name == "Downloads");
                if (defaultCategory == null)
                {
                    defaultCategory = new Category { Name = "Downloads", IsUserCreated = false };
                    dbContext.Categories.Add(defaultCategory);
                    await dbContext.SaveChangesAsync();
                }
                categoryCache["Downloads"] = defaultCategory;
            }

            // Process files found on disk
            foreach (var filePath in filesOnDisk)
            {
                var fileInfo = new FileInfo(filePath);
                var categoryName = DetermineCategoryName(filePath, _mediaRoot);

                // Use existing category if present in database; DO NOT auto-create new categories from folders on disk!
                if (!categoryCache.TryGetValue(categoryName, out var category))
                {
                    category = defaultCategory;
                }

                if (dbVideoMap.TryGetValue(filePath, out var existingVideo))
                {
                    // Check if file has changed or if it was left in an incomplete status (Processing/Discovered)
                    if (existingVideo.FileSize != fileInfo.Length || 
                        existingVideo.LastModified != fileInfo.LastWriteTimeUtc ||
                        existingVideo.ScanStatus == ScanState.Processing ||
                        existingVideo.ScanStatus == ScanState.Discovered)
                    {
                        _logger.LogInformation($"File needs processing: {filePath} (Status: {existingVideo.ScanStatus})");
                        existingVideo.FileSize = fileInfo.Length;
                        existingVideo.LastModified = fileInfo.LastWriteTimeUtc;
                        existingVideo.ScanStatus = ScanState.Processing;
                        if (existingVideo.CategoryId <= 0)
                        {
                            existingVideo.CategoryId = category.Id;
                        }
                        existingVideo.UpdatedAt = DateTime.UtcNow;
                        
                        discoveredFiles.Add(filePath);
                    }
                    else if (existingVideo.ScanStatus == ScanState.Missing)
                    {
                        // File was missing but now returned
                        _logger.LogInformation($"File returned: {filePath}. Restoring Ready status.");
                        existingVideo.ScanStatus = ScanState.Ready;
                        existingVideo.UpdatedAt = DateTime.UtcNow;
                    }
                }
                else
                {
                    // New file discovered
                    _logger.LogInformation($"New file discovered: {filePath}");
                    var newVideo = new Video
                    {
                        FilePath = filePath,
                        FileName = Path.GetFileName(filePath),
                        DisplayTitle = Path.GetFileNameWithoutExtension(filePath),
                        FileSize = fileInfo.Length,
                        LastModified = fileInfo.LastWriteTimeUtc,
                        CategoryId = category.Id,
                        ScanStatus = ScanState.Discovered
                    };
                    dbContext.Videos.Add(newVideo);
                    discoveredFiles.Add(filePath);
                }
            }

            await dbContext.SaveChangesAsync();

            // Start processing metadata and generating thumbnails asynchronously
            if (discoveredFiles.Any())
            {
                _statusMessage = $"Extracting metadata and generating thumbnails ({discoveredFiles.Count} pending)...";
                _ = Task.Run(() => ProcessPendingVideosAsync(discoveredFiles.ToList()));
            }
            else
            {
                _statusMessage = $"Scan completed. All {filesOnDisk.Count} videos are up to date.";
                _logger.LogInformation(_statusMessage);
                CleanEmptyDirectories(_mediaRoot);
                var emptyCats = await dbContext.Categories
                    .Where(c => !c.IsUserCreated && c.Name != "Downloads" && c.Name != "Radarr" && !c.Videos.Any(v => v.ScanStatus != ScanState.Missing))
                    .ToListAsync();
                if (emptyCats.Any())
                {
                    dbContext.Categories.RemoveRange(emptyCats);
                    await dbContext.SaveChangesAsync();
                }
                Interlocked.Exchange(ref _isScanning, 0);
            }
        }
        catch (Exception ex)
        {
            _statusMessage = $"Scan failed: {ex.Message}";
            _logger.LogError(ex, "Error occurred during video scan");
            Interlocked.Exchange(ref _isScanning, 0);
        }
    }

    private async Task ProcessPendingVideosAsync(List<string> filePaths)
    {
        _logger.LogInformation($"Background worker starting metadata processing for {filePaths.Count} videos...");

        int processedCount = 0;
        foreach (var filePath in filePaths)
        {
            processedCount++;
            _statusMessage = $"Processing {processedCount} of {filePaths.Count}: {Path.GetFileName(filePath)}";

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<VideoDbContext>();

            var video = await dbContext.Videos.FirstOrDefaultAsync(v => v.FilePath == filePath);
            if (video == null) continue;

            video.ScanStatus = ScanState.Processing;
            await dbContext.SaveChangesAsync();

            try
            {
                var metadata = await _mediaProcessor.ExtractMetadataAsync(filePath);
                if (metadata != null)
                {
                    video.Duration = metadata.Duration;
                    video.Width = metadata.Width;
                    video.Height = metadata.Height;
                    video.VideoCodec = metadata.VideoCodec;
                    video.AudioCodec = metadata.AudioCodec;
                    video.ContainerFormat = metadata.ContainerFormat;
                    video.FrameRate = metadata.FrameRate;
                    video.DisplayTitle = !string.IsNullOrEmpty(metadata.DisplayTitle) 
                        ? (metadata.DisplayTitle.Length > 250 ? metadata.DisplayTitle.Substring(0, 250) : metadata.DisplayTitle) 
                        : video.DisplayTitle;
                    video.AudioTracksJson = System.Text.Json.JsonSerializer.Serialize(metadata.AudioTracks);

                    // Generate Thumbnail
                    var thumbnailName = $"{video.Id}.jpg";
                    var thumbnailPath = Path.Combine(_cacheRoot, "thumbnails", thumbnailName);
                    
                    var thumbSuccess = await _mediaProcessor.GenerateThumbnailAsync(filePath, thumbnailPath, video.Duration);
                    if (thumbSuccess)
                    {
                        video.ThumbnailPath = thumbnailPath;
                    }

                    video.ScanStatus = ScanState.Ready;
                    video.ProcessingError = null;
                }
                else
                {
                    video.ScanStatus = ScanState.Failed;
                    video.ProcessingError = "Failed to extract metadata via ffprobe.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to process video {filePath}");
                video.ScanStatus = ScanState.Failed;
                video.ProcessingError = ex.Message;
            }

            video.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
        }

        _logger.LogInformation("Background metadata processing completed.");
        _statusMessage = "Idle";
        CleanEmptyDirectories(_mediaRoot);
        Interlocked.Exchange(ref _isScanning, 0);
    }

    private static string DetermineCategoryName(string filePath, string mediaRoot)
    {
        // Find relative path from mediaRoot
        var relativePath = Path.GetRelativePath(mediaRoot, filePath);
        var parts = relativePath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

        // If there is only 1 part (file directly in mediaRoot), default to Downloads
        if (parts.Length <= 1)
        {
            return "Downloads";
        }

        // If the video is inside the Downloads directory:
        // E.g. Downloads/Music/song.mp3 (Length 3) -> category is Music
        // E.g. Downloads/video.mp4 (Length 2) -> category is Downloads
        if (parts[0].Equals("Downloads", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length > 2)
            {
                return FormatCategoryName(parts[1]);
            }
            return "Downloads";
        }

        // For other top-level folders like "Movies/film.mp4"
        return FormatCategoryName(parts[0]);
    }

    private static string FormatCategoryName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return "Downloads";
        // Capitalize category name nicely
        var name = rawName.Replace('_', ' ').Replace('-', ' ').Trim();
        if (name.Length > 0)
        {
            name = char.ToUpper(name[0]) + (name.Length > 1 ? name[1..].ToLower() : string.Empty);
        }
        if (name.Length > 80)
        {
            name = name.Substring(0, 80).Trim();
        }
        return string.IsNullOrWhiteSpace(name) ? "Downloads" : name;
    }


    private void CleanEmptyDirectories(string startLocation)
    {
        try
        {
            if (!Directory.Exists(startLocation)) return;

            foreach (var directory in Directory.GetDirectories(startLocation))
            {
                CleanEmptyDirectories(directory);
                
                var normalizedDir = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var normalizedMediaRoot = Path.GetFullPath(_mediaRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var normalizedDownloads = Path.GetFullPath(Path.Combine(_mediaRoot, "Downloads")).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var normalizedRadarr = Path.GetFullPath(Path.Combine(_mediaRoot, "Radarr")).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                // Strictly protect mediaRoot, Downloads, and the separate Radarr folder from deletion
                if (normalizedDir == normalizedMediaRoot || normalizedDir == normalizedDownloads || normalizedDir == normalizedRadarr) continue;

                // Check if directory has any supported video/audio media files recursively
                var hasMediaFiles = Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
                    .Any(file => _supportedExtensions.Contains(Path.GetExtension(file).ToLower()));

                // Check if directory is inside the protected Radarr folder and contains any files (PDFs, images, subtitles, documents)
                var isInsideRadarr = normalizedDir.StartsWith(normalizedRadarr, StringComparison.OrdinalIgnoreCase);
                var hasRadarrFiles = isInsideRadarr && Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories).Any();

                // Check if directory has active download temp files modified within the last 10 minutes (.part, .ytdl)
                var hasActiveDownloads = Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
                    .Any(file => {
                        var ext = Path.GetExtension(file).ToLower();
                        if (ext == ".part" || ext == ".ytdl" || ext == ".tmp")
                        {
                            var lastWrite = File.GetLastWriteTimeUtc(file);
                            return (DateTime.UtcNow - lastWrite).TotalMinutes < 10;
                        }
                        return false;
                    });

                if (!hasMediaFiles && !hasActiveDownloads && !hasRadarrFiles)
                {
                    // Clean up all non-media junk/leftover files
                    foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            File.SetAttributes(file, FileAttributes.Normal);
                            File.Delete(file);
                        }
                        catch { }
                    }

                    try
                    {
                        Directory.Delete(directory, true);
                        _logger.LogInformation($"Cleaned up empty/abandoned directory: {directory}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Could not delete directory {directory}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to clean up empty directory {startLocation}: {ex.Message}");
        }
    }
}
