namespace MyTube.Backend.Application.Features.Sonarr;

public record SonarrSettingsResponseDto(
    string BaseUrl,
    string ApiKey,
    string? RootFolderPath,
    int QualityProfileId,
    bool IsConfigured);
