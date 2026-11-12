using System;

namespace MyTube.Backend.Application;

public enum OutputMediaType
{
    Video,
    Audio
}

public enum OutputFormat
{
    Mp4,
    Mp3,
    M4a,
    Opus,
    Wav
}

public enum MediaClassification
{
    Invalid,
    VideoWithAudio,
    VideoOnly,
    AudioOnly
}

public class MediaProbeResult
{
    public bool IsReadable { get; set; }
    public int ExitCode { get; set; }
    public string? ErrorMessage { get; set; }
    public long FileSize { get; set; }
    public TimeSpan Duration { get; set; }
    public int StreamCount { get; set; }
    public bool HasVideo { get; set; }
    public bool HasAudio { get; set; }
    public int VideoTrackCount { get; set; }
    public int AudioTrackCount { get; set; }
    public string? VideoCodec { get; set; }
    public string? AudioCodec { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public double FrameRate { get; set; }
    public string? PixelFormat { get; set; }
    public string? TimeBase { get; set; }
    public int? AudioSampleRate { get; set; }
    public int? AudioChannels { get; set; }
    public int? SampleRate { get; set; }
    public int? ChannelCount { get; set; }
    public string? ChannelLayout { get; set; }
    public string? ContainerFormat { get; set; }

    public MediaClassification Classification
    {
        get
        {
            if (HasVideo && HasAudio) return MediaClassification.VideoWithAudio;
            if (HasVideo && !HasAudio) return MediaClassification.VideoOnly;
            if (!HasVideo && HasAudio) return MediaClassification.AudioOnly;
            return MediaClassification.Invalid;
        }
    }
}

public class MediaValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public bool HasExpectedStreams { get; set; }
    public bool IsDecodable { get; set; }
    public double DurationDifferenceSeconds { get; set; }
    public TimeSpan ActualDuration { get; set; }
    public TimeSpan ExpectedDuration { get; set; }
    public TimeSpan DurationDifference => ActualDuration > ExpectedDuration 
        ? ActualDuration - ExpectedDuration 
        : ExpectedDuration - ActualDuration;
}

public class MediaProcessRequest
{
    public Guid JobId { get; set; }
    public string InputPath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public OutputMediaType OutputMediaType { get; set; } = OutputMediaType.Video;
    public OutputFormat OutputFormat { get; set; } = OutputFormat.Mp4;
    public TimeSpan OriginalDuration { get; set; }
    public TimeSpan ExpectedDuration { get; set; }
    public string? AudioBitrate { get; set; }
    public MediaProcessingOptions? Options { get; set; }
    public Action<double>? OnProgressPercent { get; set; }
}

public class MediaProcessResult
{
    public bool Success { get; set; }
    public string? OutputPath { get; set; }
    public string? FallbackReason { get; set; }
    public string? ErrorMessage { get; set; }
    public string? StandardError { get; set; }
    public int ExitCode { get; set; }
    public TimeSpan ActualDuration { get; set; }
    public string? ProcessingStrategyUsed { get; set; }
    public MediaProcessingException? Exception { get; set; }
}
