using System.Collections.Generic;

namespace MyTube.Backend.Application.Features.Radarr;

public record RadarrMovieDownloadResult(string FilePath, string ContentType, string FileName);

public record RadarrTargetDownloadResult(string ResolvedPath, bool IsDirectory, string ContentType, string FileName);

public record RadarrTorrentDownloadResult(string? TargetPath, bool IsDirectory, List<string> Files, string DownloadName, string? ErrorMessage);

public record RadarrMediaImageResult(byte[] ImageBytes, string ContentType, string FileName);

public record RadarrSettingsResponseDto(
    string BaseUrl,
    string ApiKey,
    string? RootFolderPath,
    int QualityProfileId,
    bool IsConfigured);
