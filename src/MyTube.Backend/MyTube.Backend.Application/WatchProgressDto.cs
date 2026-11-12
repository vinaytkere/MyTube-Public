using System;

namespace MyTube.Backend.Application;

/// <summary>
/// Data transfer record for video watch progress.
/// </summary>
public sealed record WatchProgressDto
{
    public Guid VideoId { get; init; }
    public double PositionSeconds { get; init; }
    public double DurationSeconds { get; init; }
    public double PercentComplete => DurationSeconds > 0 ? (PositionSeconds / DurationSeconds) * 100 : 0;
    public bool Completed { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
