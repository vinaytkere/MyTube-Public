using System;

namespace MyTube.Backend.Domain;

public class Video
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string DisplayTitle { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime LastModified { get; set; }
    public double Duration { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string VideoCodec { get; set; } = string.Empty;
    public string AudioCodec { get; set; } = string.Empty;
    public string ContainerFormat { get; set; } = string.Empty;
    public double FrameRate { get; set; }
    public string ThumbnailPath { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
    
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    
    public ScanState ScanStatus { get; set; } = ScanState.Discovered;
    public string? ProcessingError { get; set; }
    public string? AudioTracksJson { get; set; }
    
    // AI Metadata & Transcription
    public string? Transcript { get; set; }
    public string? Summary { get; set; }
    public string? TagsJson { get; set; }
    public string? DetectedLanguage { get; set; }
    public string AiStatus { get; set; } = "NotProcessed"; // NotProcessed, Processing, Completed, Failed
    public DateTime? AiProcessedAt { get; set; }

    // Media Organizer & Classification
    public string? RawDescription { get; set; }
    public string? Uploader { get; set; }
    public string? MediaTypeDetected { get; set; }
    public string? PrimaryGenre { get; set; }
    public int? ReleaseYear { get; set; }
    public bool IsOrganized { get; set; }
    public string? OrganizedPath { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
