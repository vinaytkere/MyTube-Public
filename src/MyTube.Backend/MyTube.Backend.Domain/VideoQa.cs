using System;

namespace MyTube.Backend.Domain;

public class VideoQa
{
    public int Id { get; set; }
    public Guid VideoId { get; set; }
    public Video? Video { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string? FirecrawlContext { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
