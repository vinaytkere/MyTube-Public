using System;
using System.Collections.Generic;

namespace MyTube.Backend.Domain;

public enum DownloadStatus
{
    Queued,
    Downloading,
    Downloaded,
    Probing,
    Processing,
    Validating,
    Publishing,
    Completed,
    DownloadFailed,
    ProbeFailed,
    ProcessingFailed,
    ValidationFailed,
    PublishFailed,
    Failed,
    Cancelled
}

public class DownloadBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? CategoryName { get; set; }
    public int TotalItems { get; set; }
    public int CompletedItems { get; set; }
    public int FailedItems { get; set; }
    public DownloadStatus Status { get; set; } = DownloadStatus.Queued;

    public List<DownloadJobRecord> Jobs { get; set; } = new();
}

public class DownloadJobRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BatchId { get; set; }
    public DownloadBatch? Batch { get; set; }

    public string Url { get; set; } = string.Empty;
    public string Portal { get; set; } = "Generic";
    public string Title { get; set; } = "Fetching video details...";
    public string? CategoryName { get; set; }

    public DownloadStatus Status { get; set; } = DownloadStatus.Queued;
    public string Phase { get; set; } = "Queued";
    public double Progress { get; set; }
    public string Speed { get; set; } = string.Empty;
    public string Eta { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? FilePath { get; set; }

    public string MediaType { get; set; } = "Video";
    public string Format { get; set; } = "Mp4";
    public string? Quality { get; set; }
    public bool IncludeSubtitles { get; set; }
    public string? SubtitleLanguage { get; set; }
}
