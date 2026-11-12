namespace MyTube.Backend.Application.Radarr;

public class RadarrSettings
{
    public string BaseUrl { get; set; } = "http://localhost:7878";
    public string ApiKey { get; set; } = string.Empty;
    public string? RootFolderPath { get; set; }
    public int QualityProfileId { get; set; } = 1;
}
