namespace MyTube.Backend.Application;

public class VideoMetadata
{
    public double Duration { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string VideoCodec { get; set; } = string.Empty;
    public string AudioCodec { get; set; } = string.Empty;
    public string ContainerFormat { get; set; } = string.Empty;
    public double FrameRate { get; set; }
    public string DisplayTitle { get; set; } = string.Empty;
    public System.Collections.Generic.List<AudioTrackInfo> AudioTracks { get; set; } = new();
}

public class AudioTrackInfo
{
    public int Index { get; set; }
    public string Language { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Codec { get; set; } = string.Empty;
}
