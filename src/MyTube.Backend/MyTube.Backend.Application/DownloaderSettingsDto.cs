namespace MyTube.Backend.Application;

public sealed record DownloaderSettingsDto
{
    public bool GeoBypass { get; set; } = true;
    public string Xff { get; set; } = "default";
    public string? GeoVerificationProxy { get; set; }
    public string? Proxy { get; set; }
    public bool ForceIpv4 { get; set; } = true;
    public string? CookiesPath { get; set; }
    public bool HasCookiesFile { get; set; }
    public string? CookiesResolvedPath { get; set; }
    public string? ExtractorArgs { get; set; } = "";
    public string? YtDlpVersion { get; set; }
}
