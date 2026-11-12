using System;
using System.Collections.Generic;
using MyTube.Backend.Domain;

namespace MyTube.Backend.Application;

public class BatchDownloadRequest
{
    public List<string> Urls { get; set; } = new();
    public string? CategoryName { get; set; }
    public string? Quality { get; set; }
    public OutputMediaType MediaType { get; set; } = OutputMediaType.Video;
    public OutputFormat? Format { get; set; }
    public bool IncludeSubtitles { get; set; }
    public string? SubtitleLanguage { get; set; }
    public string? AudioLanguage { get; set; }
    public bool ExpandPlaylists { get; set; } = true;
    public bool AudioOnly { get; set; }
}

public class BatchDownloadResponseDto
{
    public Guid BatchId { get; set; }
    public DownloadStatus Status { get; set; } = DownloadStatus.Queued;
    public int EnqueuedJobsCount { get; set; }
    public List<DownloadJobRecordDto> Jobs { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

public class DownloadBatchDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CategoryName { get; set; }
    public int TotalItems { get; set; }
    public int CompletedItems { get; set; }
    public int FailedItems { get; set; }
    public DownloadStatus Status { get; set; }
    public List<DownloadJobRecordDto> Jobs { get; set; } = new();
}

public class DownloadJobRecordDto
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Portal { get; set; } = "Generic";
    public string Title { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public DownloadStatus Status { get; set; }
    public string Phase { get; set; } = string.Empty;
    public double Progress { get; set; }
    public string Speed { get; set; } = string.Empty;
    public string Eta { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? FilePath { get; set; }
    public string MediaType { get; set; } = "Video";
    public string Format { get; set; } = "Mp4";
    public string? Quality { get; set; }
    public bool IncludeSubtitles { get; set; }
    public string? SubtitleLanguage { get; set; }
    public string? AudioLanguage { get; set; }
}

public class PlaylistProbeResultDto
{
    public string Title { get; set; } = string.Empty;
    public string? Id { get; set; }
    public string? Uploader { get; set; }
    public int ItemCount => Items.Count;
    public List<PlaylistProbeItemDto> Items { get; set; } = new();
}

public class PlaylistProbeItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public double? DurationSeconds { get; set; }
    public string? FormattedDuration { get; set; }
}
