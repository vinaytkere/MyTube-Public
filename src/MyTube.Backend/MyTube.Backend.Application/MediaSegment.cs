using System;

namespace MyTube.Backend.Application;

public class MediaSegment
{
    public int Index { get; set; }
    public TimeSpan Start { get; set; }
    public TimeSpan? End { get; set; }
    public TimeSpan Duration => End.HasValue && End.Value > Start ? End.Value - Start : TimeSpan.Zero;
}

public class NormalizedMediaSegment
{
    public int OriginalIndex { get; set; }
    public int NormalizedIndex { get; set; }
    public double StartSeconds { get; set; }
    public double EndSeconds { get; set; }
    public double DurationSeconds => EndSeconds - StartSeconds;

    public TimeSpan Start => TimeSpan.FromSeconds(StartSeconds);
    public TimeSpan End => TimeSpan.FromSeconds(EndSeconds);
    public TimeSpan Duration => TimeSpan.FromSeconds(DurationSeconds);

    public override string ToString() =>
        $"{StartSeconds:F3}s - {EndSeconds:F3}s ({DurationSeconds:F3}s)";
}
