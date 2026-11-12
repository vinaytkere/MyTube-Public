using System;

namespace MyTube.Backend.Application;

public enum OutputProfile
{
    OriginalCompatible,
    Mp3_192k,
    Aac_Standard,
    Mp4_H264_Aac,
    Webm_Opus,
    AudioOnly
}

public enum ProcessingMode
{
    Auto,
    ForceStreamCopy,
    ForceFilterReencode
}

public enum OverlapPolicy
{
    Merge,
    Reject
}

public enum OutOfRangePolicy
{
    Clamp,
    Reject
}

public class MediaProcessingOptions
{
    public OutputProfile Profile { get; set; } = OutputProfile.OriginalCompatible;
    public ProcessingMode Mode { get; set; } = ProcessingMode.Auto;
    public int? AudioSampleRate { get; set; }
    public int? AudioChannels { get; set; }
    public string? AudioBitrate { get; set; }
    public string? VideoPreset { get; set; } = "veryfast";
    public int? VideoCrf { get; set; } = 22;
    public int VideoTrackIndex { get; set; } = 0;
    public int AudioTrackIndex { get; set; } = 0;
    public bool PreserveMetadata { get; set; } = true;
    public OverlapPolicy OverlapHandling { get; set; } = OverlapPolicy.Merge;
    public OutOfRangePolicy OutOfRangeHandling { get; set; } = OutOfRangePolicy.Clamp;
    public int MaxAllowedSegments { get; set; } = 100;
    public TimeSpan MaxAllowedDuration { get; set; } = TimeSpan.FromHours(4);
}
