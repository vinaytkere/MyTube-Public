using System;

namespace MyTube.Backend.Domain;

public class WatchProgress
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid VideoId { get; set; }
    public Video? Video { get; set; }
    public double PositionSeconds { get; set; }
    public double DurationSeconds { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool Completed { get; set; }
}
