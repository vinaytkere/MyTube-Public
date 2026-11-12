using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyTube.Backend.Application;

namespace MyTube.Backend.Infrastructure.Media;

public class MediaToolHealthResult
{
    public bool IsHealthy { get; set; }
    public string? FfmpegVersion { get; set; }
    public string? FfprobeVersion { get; set; }
    public string? YtDlpVersion { get; set; }
    public bool IsStagingWritable { get; set; }
    public bool IsMediaRootWritable { get; set; }
    public long FreeDiskSpaceBytes { get; set; }
    public string? ErrorMessage { get; set; }
}

public class MediaHealthCheck
{
    private readonly IMediaToolRunner _toolRunner;
    private readonly ILogger<MediaHealthCheck> _logger;

    public MediaHealthCheck(IMediaToolRunner toolRunner, ILogger<MediaHealthCheck> logger)
    {
        _toolRunner = toolRunner;
        _logger = logger;
    }

    public async Task<MediaToolHealthResult> CheckHealthAsync(string stagingPath, string mediaRootPath, CancellationToken token = default)
    {
        var result = new MediaToolHealthResult();

        try
        {
            // 1. Check FFmpeg
            var ffmpegRes = await _toolRunner.RunToolAsync("ffmpeg", new[] { "-version" }, token, timeout: TimeSpan.FromSeconds(10));
            if (ffmpegRes.ExitCode == 0 && !string.IsNullOrWhiteSpace(ffmpegRes.StandardOutput))
            {
                result.FfmpegVersion = ffmpegRes.StandardOutput.Split('\n')[0].Trim();
            }
            else
            {
                result.ErrorMessage = $"FFmpeg check failed (code {ffmpegRes.ExitCode}): {ffmpegRes.StandardError}";
            }

            // 2. Check FFprobe
            var ffprobeRes = await _toolRunner.RunToolAsync("ffprobe", new[] { "-version" }, token, timeout: TimeSpan.FromSeconds(10));
            if (ffprobeRes.ExitCode == 0 && !string.IsNullOrWhiteSpace(ffprobeRes.StandardOutput))
            {
                result.FfprobeVersion = ffprobeRes.StandardOutput.Split('\n')[0].Trim();
            }
            else
            {
                result.ErrorMessage = (result.ErrorMessage ?? "") + $" FFprobe check failed: {ffprobeRes.StandardError}";
            }

            // 3. Check yt-dlp
            var ytdlpRes = await _toolRunner.RunToolAsync("yt-dlp", new[] { "--version" }, token, timeout: TimeSpan.FromSeconds(10));
            if (ytdlpRes.ExitCode == 0 && !string.IsNullOrWhiteSpace(ytdlpRes.StandardOutput))
            {
                result.YtDlpVersion = ytdlpRes.StandardOutput.Trim();
            }
            else
            {
                result.ErrorMessage = (result.ErrorMessage ?? "") + $" yt-dlp check failed: {ytdlpRes.StandardError}";
            }

            // 4. Check Staging writable
            try
            {
                if (!Directory.Exists(stagingPath)) Directory.CreateDirectory(stagingPath);
                var testFile = Path.Combine(stagingPath, $".health_test_{Guid.NewGuid():N}.tmp");
                await File.WriteAllTextAsync(testFile, "ok", token);
                File.Delete(testFile);
                result.IsStagingWritable = true;
            }
            catch (Exception ex)
            {
                result.IsStagingWritable = false;
                result.ErrorMessage = (result.ErrorMessage ?? "") + $" Staging not writable: {ex.Message}";
            }

            // 5. Check Media Root writable
            try
            {
                if (!Directory.Exists(mediaRootPath)) Directory.CreateDirectory(mediaRootPath);
                var testFile = Path.Combine(mediaRootPath, $".health_test_{Guid.NewGuid():N}.tmp");
                await File.WriteAllTextAsync(testFile, "ok", token);
                File.Delete(testFile);
                result.IsMediaRootWritable = true;
            }
            catch (Exception ex)
            {
                result.IsMediaRootWritable = false;
                result.ErrorMessage = (result.ErrorMessage ?? "") + $" Media root not writable: {ex.Message}";
            }

            // 6. Free disk space
            try
            {
                var driveInfo = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(stagingPath)) ?? "C:\\");
                result.FreeDiskSpaceBytes = driveInfo.AvailableFreeSpace;
            }
            catch { }

            result.IsHealthy = !string.IsNullOrEmpty(result.FfmpegVersion) &&
                               !string.IsNullOrEmpty(result.FfprobeVersion) &&
                               !string.IsNullOrEmpty(result.YtDlpVersion) &&
                               result.IsStagingWritable &&
                               result.IsMediaRootWritable;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Media tool health check encountered unhandled exception.");
            result.ErrorMessage = ex.Message;
            result.IsHealthy = false;
            return result;
        }
    }
}
