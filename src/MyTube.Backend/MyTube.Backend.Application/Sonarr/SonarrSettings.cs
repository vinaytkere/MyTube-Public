namespace MyTube.Backend.Application.Sonarr;

public class SonarrSettings
{
    public string BaseUrl { get; set; } = "http://localhost:8989";
    public string ApiKey { get; set; } = string.Empty;
    public string? RootFolderPath { get; set; }
    public int QualityProfileId { get; set; } = 1;
}

public class SonarrTestConnectionRequest
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}
