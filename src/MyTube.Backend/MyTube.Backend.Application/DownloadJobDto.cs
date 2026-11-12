using System;
using MyTube.Backend.Domain;

namespace MyTube.Backend.Application;

/// <summary>
/// Data transfer record for asynchronous media download jobs.
/// </summary>
public sealed record DownloadJobDto
{
    public Guid Id { get; set; }
    public Guid? BatchId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = "Fetching video details...";
    public string CategoryName { get; set; } = string.Empty;
    public DownloadStatus Status { get; set; }
    public string Phase { get; set; } = string.Empty;
    public double Progress { get; set; } // 0 to 100
    public string Speed { get; set; } = string.Empty;
    public string Eta { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public string? DiagnosticReference { get; set; }
    public string? WorkerId { get; set; }
    public string? ProcessingStrategyUsed { get; set; }
    public string? FallbackReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? FilePath { get; set; }
    public OutputMediaType MediaType { get; set; } = OutputMediaType.Video;
    public OutputFormat Format { get; set; } = OutputFormat.Mp4;
    public double? ExpectedDurationSeconds { get; set; }
    public double? ActualDurationSeconds { get; set; }
    public long? SourceFileSize { get; set; }
    public long? OutputFileSize { get; set; }
    public bool IncludeSubtitles { get; set; }
    public string? SubtitleLanguage { get; set; }
    public string? AudioLanguage { get; set; }
}
