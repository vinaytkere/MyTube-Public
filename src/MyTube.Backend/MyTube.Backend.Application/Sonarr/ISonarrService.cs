using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyTube.Backend.Application.Sonarr;

public interface ISonarrService
{
    Task<SonarrStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<List<SonarrSeriesDto>> SearchSeriesAsync(string query, CancellationToken cancellationToken = default);
    Task<List<SonarrSeriesDto>> GetLibrarySeriesAsync(CancellationToken cancellationToken = default);
    Task<SonarrSeriesDto?> GetSeriesByIdAsync(int seriesId, CancellationToken cancellationToken = default);
    Task<List<SonarrEpisodeDto>> GetEpisodesAsync(int seriesId, CancellationToken cancellationToken = default);
    Task<SonarrSeriesDto?> AddSeriesAsync(SonarrAddSeriesRequest request, CancellationToken cancellationToken = default);
    Task<List<SonarrReleaseDto>> GetReleasesAsync(int seriesId, int? episodeId = null, CancellationToken cancellationToken = default);
    Task<bool> DownloadReleaseAsync(SonarrDownloadReleaseRequest request, CancellationToken cancellationToken = default);
    Task<List<SonarrQueueItemDto>> GetQueueAsync(CancellationToken cancellationToken = default);
    Task<List<SonarrQualityProfileDto>> GetQualityProfilesAsync(CancellationToken cancellationToken = default);
    Task<List<SonarrRootFolderDto>> GetRootFoldersAsync(CancellationToken cancellationToken = default);
    Task<SonarrStatusDto> TestConnectionAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default);
    Task UpdateSettingsAsync(SonarrSettings newSettings, CancellationToken cancellationToken = default);
    SonarrSettings GetCurrentSettings();
}
