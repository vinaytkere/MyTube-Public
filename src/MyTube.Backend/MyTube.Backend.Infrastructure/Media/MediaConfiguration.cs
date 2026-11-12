using System;

namespace MyTube.Backend.Infrastructure.Media;

public class MediaConfiguration
{
    public string FfmpegPath { get; set; } = "ffmpeg";
    public string FfprobePath { get; set; } = "ffprobe";
    public string YtDlpPath { get; set; } = "yt-dlp";
    public string WorkingDirectory { get; set; } = string.Empty;
    public string MediaRootPath { get; set; } = "./media";
    public string StagingPath { get; set; } = "./staging";
    public int MaxProcessingSeconds { get; set; } = 900; // 15 minutes
    public int MaxConcurrentJobs { get; set; } = 3;
    public int MaxSegments { get; set; } = 100;
    public long MinimumFreeDiskSpaceBytes { get; set; } = 1024L * 1024L * 1024L; // 1 GB
    public long MaximumSourceFileSize { get; set; } = 10L * 1024L * 1024L * 1024L; // 10 GB
    public TimeSpan MaximumOutputDuration { get; set; } = TimeSpan.FromHours(4);

    // Restrictions & Geo-Bypass settings
    public bool GeoBypass { get; set; } = true;
    public string Xff { get; set; } = "default";
    public string? GeoVerificationProxy { get; set; }
    public string? Proxy { get; set; }
    public bool ForceIpv4 { get; set; } = true;
    public string? CookiesPath { get; set; }
    public string? ExtractorArgs { get; set; } = "";
}
