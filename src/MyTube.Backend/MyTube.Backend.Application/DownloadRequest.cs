namespace MyTube.Backend.Application;

public class DownloadRequest
{
    public string Url { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string? Quality { get; set; }
    public OutputMediaType MediaType { get; set; } = OutputMediaType.Video;
    public OutputFormat? Format { get; set; }
    public string? AudioBitrate { get; set; }
    public int? AudioSampleRate { get; set; }
    public int? AudioChannels { get; set; }
    public bool IncludeSubtitles { get; set; }
    public string? SubtitleLanguage { get; set; }
    public string? AudioLanguage { get; set; }
}
