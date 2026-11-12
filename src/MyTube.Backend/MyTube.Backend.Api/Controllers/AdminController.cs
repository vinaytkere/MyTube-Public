using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MediatR;
using MyTube.Backend.Application;
using MyTube.Backend.Application.Features.Categories.Commands;
using MyTube.Backend.Application.Features.Videos.Commands;
using MyTube.Backend.Application.Radarr;
using MyTube.Backend.Application.Sonarr;
using MyTube.Backend.Domain;
using MyTube.Backend.Infrastructure;
using MyTube.Backend.Infrastructure.Media;

namespace MyTube.Backend.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrator")]
[Produces("application/json")]
public class AdminController : ControllerBase
{
    private readonly IVideoScanner _videoScanner;
    private readonly ISender _sender;
    private readonly VideoDbContext _dbContext;
    private readonly MediaHealthCheck _mediaHealthCheck;
    private readonly IRadarrService _radarrService;
    private readonly ISonarrService _sonarrService;
    private readonly IConfiguration _configuration;

    public AdminController(
        IVideoScanner videoScanner,
        ISender sender,
        VideoDbContext dbContext,
        MediaHealthCheck mediaHealthCheck,
        IRadarrService radarrService,
        ISonarrService sonarrService,
        IConfiguration configuration)
    {
        _videoScanner = videoScanner;
        _sender = sender;
        _dbContext = dbContext;
        _mediaHealthCheck = mediaHealthCheck;
        _radarrService = radarrService;
        _sonarrService = sonarrService;
        _configuration = configuration;
    }

    [HttpPost("scan")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult TriggerScan()
    {
        _ = Task.Run(() => _videoScanner.ScanAllAsync());
        return Ok(new { message = "Scan triggered successfully in background." });
    }

    [HttpGet("scan/status")]
    public IActionResult GetScanStatus()
    {
        return Ok(new
        {
            isScanning = _videoScanner.IsScanning,
            statusMessage = _videoScanner.ScanStatusMessage
        });
    }

    [HttpGet("system-health")]
    public async Task<IActionResult> GetSystemHealth(CancellationToken cancellationToken)
    {
        var mediaRoot = _configuration["Media:RootPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "media");
        var cacheRoot = _configuration["Media:CachePath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "cache");

        // 1. Storage Drive Diagnostics
        object storageInfo;
        try
        {
            var fullPath = Path.GetFullPath(mediaRoot);
            var driveInfo = new DriveInfo(Path.GetPathRoot(fullPath) ?? fullPath);
            var totalBytes = driveInfo.TotalSize;
            var freeBytes = driveInfo.AvailableFreeSpace;
            var usedBytes = totalBytes - freeBytes;
            var usedPercent = totalBytes > 0 ? Math.Round((usedBytes / (double)totalBytes) * 100, 1) : 0;

            storageInfo = new
            {
                driveName = driveInfo.Name,
                driveFormat = driveInfo.DriveFormat,
                totalSizeBytes = totalBytes,
                freeSizeBytes = freeBytes,
                usedSizeBytes = usedBytes,
                usedPercentage = usedPercent,
                formattedTotal = FormatBytes(totalBytes),
                formattedFree = FormatBytes(freeBytes),
                formattedUsed = FormatBytes(usedBytes),
                mediaPath = fullPath
            };
        }
        catch (Exception ex)
        {
            storageInfo = new { error = ex.Message, mediaPath = mediaRoot };
        }

        // 2. Media Tools Check (FFmpeg, FFprobe)
        MediaToolHealthResult toolHealth;
        try
        {
            toolHealth = await _mediaHealthCheck.CheckHealthAsync(cacheRoot, mediaRoot, cancellationToken);
        }
        catch
        {
            toolHealth = new MediaToolHealthResult { ErrorMessage = "Health check failed." };
        }

        // 3. Database Check & Library Aggregates
        bool dbHealthy = false;
        int totalVideos = 0;
        int readyVideos = 0;
        int processingVideos = 0;
        int failedVideos = 0;
        long totalMediaBytes = 0;

        try
        {
            dbHealthy = await _dbContext.Database.CanConnectAsync(cancellationToken);
            if (dbHealthy)
            {
                var stats = await _dbContext.Videos
                    .GroupBy(v => v.ScanStatus)
                    .Select(g => new { Status = g.Key, Count = g.Count(), TotalBytes = g.Sum(x => x.FileSize) })
                    .ToListAsync(cancellationToken);

                totalVideos = stats.Sum(s => s.Count);
                readyVideos = stats.FirstOrDefault(s => s.Status == ScanState.Ready)?.Count ?? 0;
                processingVideos = stats.FirstOrDefault(s => s.Status == ScanState.Processing || s.Status == ScanState.Discovered)?.Count ?? 0;
                failedVideos = stats.FirstOrDefault(s => s.Status == ScanState.Failed || s.Status == ScanState.Missing)?.Count ?? 0;
                totalMediaBytes = stats.Sum(s => s.TotalBytes);
            }
        }
        catch { }

        // 4. Radarr & Sonarr Status
        var radarrStatus = await _radarrService.GetStatusAsync(cancellationToken);
        var sonarrStatus = await _sonarrService.GetStatusAsync(cancellationToken);

        return Ok(new
        {
            timestamp = DateTime.UtcNow,
            storage = storageInfo,
            mediaTools = new
            {
                ffmpegVersion = toolHealth.FfmpegVersion,
                ffprobeVersion = toolHealth.FfprobeVersion,
                ffmpegHealthy = !string.IsNullOrEmpty(toolHealth.FfmpegVersion),
                ffprobeHealthy = !string.IsNullOrEmpty(toolHealth.FfprobeVersion),
                isMediaRootWritable = toolHealth.IsMediaRootWritable,
                errorMessage = toolHealth.ErrorMessage
            },
            services = new
            {
                database = new { connected = dbHealthy, status = dbHealthy ? "Healthy" : "Offline" },
                radarr = new { isConfigured = radarrStatus.IsConfigured, isConnected = radarrStatus.IsConnected, version = radarrStatus.Version },
                sonarr = new { isConfigured = sonarrStatus.IsConfigured, isConnected = sonarrStatus.IsConnected, version = sonarrStatus.Version },
                fileWatcher = new { active = true, status = "Monitoring Active" }
            },
            library = new
            {
                totalVideos,
                readyVideos,
                processingVideos,
                failedVideos,
                totalSizeBytes = totalMediaBytes,
                formattedTotalSize = FormatBytes(totalMediaBytes)
            }
        });
    }

    [HttpPost("videos/delete-batch")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteVideosBatch([FromBody] DeleteVideosRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new DeleteVideosBatchCommand(request.VideoIds), ct);
        return Ok(new { message = "Delete operation completed successfully.", deletedCount = result });
    }

    [HttpPut("videos/{id}/rename")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RenameVideo(Guid id, [FromBody] RenameVideoRequest request, CancellationToken ct)
    {
        var success = await _sender.Send(new RenameVideoCommand(id, request.NewTitle), ct);
        if (!success)
        {
            return NotFound(new { message = "Video not found or rename failed." });
        }

        return Ok(new { message = "Video renamed successfully." });
    }

    [HttpPost("categories/{id}/delete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken ct)
    {
        var success = await _sender.Send(new DeleteCategoryCommand(id), ct);
        if (!success)
        {
            return NotFound(new { message = "Category not found or deletion failed." });
        }

        return Ok(new { message = "Category and its folder deleted successfully." });
    }

    [HttpPost("system/shutdown")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ShutdownServer()
    {
        var mediaRoot = _configuration["Media:RootPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "media");
        var triggerPath = Path.Combine(mediaRoot, ".poweroff_trigger");

        try
        {
            await System.IO.File.WriteAllTextAsync(triggerPath, $"shutdown_requested_at_{DateTime.UtcNow:O}\n");
            _ = Task.Run(async () =>
            {
                await Task.Delay(1500);
                try { System.Diagnostics.Process.Start("poweroff"); } catch { }
            });
            return Ok(new { message = "Homelab physical server shutdown initiated. The system will power down in a few seconds." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to initiate shutdown: {ex.Message}" });
        }
    }

    [HttpPost("system/reboot")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RebootServer()
    {
        var mediaRoot = _configuration["Media:RootPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "media");
        var triggerPath = Path.Combine(mediaRoot, ".reboot_trigger");

        try
        {
            await System.IO.File.WriteAllTextAsync(triggerPath, $"reboot_requested_at_{DateTime.UtcNow:O}\n");
            _ = Task.Run(async () =>
            {
                await Task.Delay(1500);
                try { System.Diagnostics.Process.Start("reboot"); } catch { }
            });
            return Ok(new { message = "Homelab physical server reboot initiated. The system will restart shortly." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to initiate reboot: {ex.Message}" });
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }
}

public sealed record DeleteVideosRequest
{
    public List<Guid> VideoIds { get; init; } = new();
}

public sealed record RenameVideoRequest
{
    public string NewTitle { get; init; } = string.Empty;
}

