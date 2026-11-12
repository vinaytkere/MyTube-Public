using System;
using System.Collections.Generic;

namespace MyTube.Backend.Application;

/// <summary>
/// Data transfer record representing a video and its metadata.
/// </summary>
public sealed record VideoDto
{
    public Guid Id { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string DisplayTitle { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public double Duration { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public string VideoCodec { get; init; } = string.Empty;
    public string AudioCodec { get; init; } = string.Empty;
    public string ContainerFormat { get; init; } = string.Empty;
    public double FrameRate { get; init; }
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public string ScanStatus { get; init; } = string.Empty;
    public string? SourceUrl { get; init; }
    public string Quality { get; init; } = string.Empty;
    
    // Virtual URLs (constructed dynamically by API depending on request host)
    public string StreamUrl { get; init; } = string.Empty;
    public string DownloadUrl { get; init; } = string.Empty;
    public string ThumbnailUrl { get; init; } = string.Empty;
    
    // User progress details (populated only when queried in context of a user)
    public double? ProgressSeconds { get; init; }
    public bool? Completed { get; init; }
    public bool IsFavorite { get; init; }

    // AI Metadata & Transcription
    public string? Transcript { get; init; }
    public string? Summary { get; init; }
    public List<string> Tags { get; init; } = new();
    public string? DetectedLanguage { get; init; }
    public string AiStatus { get; init; } = "NotProcessed";
    public string? ProcessingError { get; init; }
    public DateTimeOffset? AiProcessedAt { get; init; }
    
    public DateTimeOffset CreatedAt { get; init; }
    
    // Media Organizer Fields
    public string? MediaTypeDetected { get; init; }
    public string? PrimaryGenre { get; init; }
    public int? ReleaseYear { get; init; }
    public bool IsOrganized { get; init; }
    public string? OrganizedPath { get; init; }
    public string? Uploader { get; init; }

    public List<AudioTrackDto> AudioTracks { get; init; } = new();
    public List<SubtitleTrackDto> SubtitleTracks { get; init; } = new();
}

/// <summary>
/// Data transfer record for an audio track in a media item.
/// </summary>
public sealed record AudioTrackDto
{
    public int Index { get; init; }
    public string Language { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Codec { get; init; } = string.Empty;
}

/// <summary>
/// Data transfer record for a subtitle track in a media item.
/// </summary>
public sealed record SubtitleTrackDto
{
    public string Id { get; init; } = string.Empty;
    public string Src { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Language { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
}
