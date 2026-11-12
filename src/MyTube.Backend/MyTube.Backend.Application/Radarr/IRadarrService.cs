using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyTube.Backend.Application.Radarr;

public interface IRadarrService
{
    Task<RadarrStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<List<RadarrMovieDto>> SearchMoviesAsync(string query, CancellationToken cancellationToken = default);
    Task<List<RadarrMovieDto>> GetLibraryMoviesAsync(CancellationToken cancellationToken = default);
    Task<RadarrMovieDto?> AddMovieAsync(RadarrAddMovieRequest request, CancellationToken cancellationToken = default);
    Task<RadarrAutoGrabResultDto> AutoGrabMovieAsync(RadarrAddMovieRequest request, CancellationToken cancellationToken = default);
    Task<List<RadarrReleaseDto>> GetReleasesAsync(int movieId, CancellationToken cancellationToken = default);
    Task<bool> DownloadReleaseAsync(RadarrDownloadReleaseRequest request, CancellationToken cancellationToken = default);
    Task<List<RadarrQueueItemDto>> GetQueueAsync(CancellationToken cancellationToken = default);
    Task<bool> CancelQueueItemAsync(int id, int? transmissionId = null, bool deleteData = true, CancellationToken cancellationToken = default);
    Task<bool> PauseQueueItemAsync(int id, int? transmissionId = null, CancellationToken cancellationToken = default);
    Task<bool> ResumeQueueItemAsync(int id, int? transmissionId = null, CancellationToken cancellationToken = default);
    Task<bool> KickstartQueueItemAsync(int id, int? transmissionId = null, CancellationToken cancellationToken = default);
    Task<List<RadarrQualityProfileDto>> GetQualityProfilesAsync(CancellationToken cancellationToken = default);
    Task<List<RadarrRootFolderDto>> GetRootFoldersAsync(CancellationToken cancellationToken = default);
    Task<RadarrStatusDto> TestConnectionAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default);
    Task UpdateSettingsAsync(RadarrSettings newSettings, CancellationToken cancellationToken = default);
    RadarrSettings GetCurrentSettings();
    Task<RadarrDownloadSummaryDto> GetDownloadedMediaSummaryAsync(CancellationToken cancellationToken = default);
    Task<bool> SyncRadarrMediaLinksAsync(CancellationToken cancellationToken = default);
    Task<bool> DeleteMovieFilesAsync(int movieId, CancellationToken cancellationToken = default);
    Task<int> DeleteAllDownloadedFilesAsync(CancellationToken cancellationToken = default);
    Task<int> BatchDeleteMovieFilesAsync(List<int> movieIds, List<string>? filePaths = null, CancellationToken cancellationToken = default);
    Task<(string FilePath, string ContentType, string FileName)?> GetMovieDownloadFileAsync(int movieId, CancellationToken cancellationToken = default);
    Task<(string FilePath, string ContentType, string FileName)?> GetFileDownloadAsync(string filePath, CancellationToken cancellationToken = default);
    Task<(string ResolvedPath, bool IsDirectory, string ContentType, string FileName)?> GetDownloadTargetAsync(string path, CancellationToken cancellationToken = default);
    Task<(string? TargetPath, bool IsDirectory, List<string> Files, string DownloadName, string? ErrorMessage)?> GetTransmissionDownloadInfoAsync(int transmissionId, int? fileIndex = null, int? movieId = null, CancellationToken cancellationToken = default);
    Task<(byte[] ImageBytes, string ContentType, string FileName)?> GetMoviePosterDownloadAsync(int movieId, CancellationToken cancellationToken = default);
    Task<(byte[] ImageBytes, string ContentType, string FileName)?> GetMovieFanartDownloadAsync(int movieId, CancellationToken cancellationToken = default);
}

