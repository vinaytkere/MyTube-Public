using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyTube.Backend.Application.Prowlarr;

public interface IProwlarrService
{
    Task<List<ProwlarrSearchResultDto>> SearchAsync(string query, string? category = null, CancellationToken cancellationToken = default);
    Task<bool> DownloadAsync(ProwlarrDownloadRequest request, CancellationToken cancellationToken = default);
    Task<List<TransmissionTorrentDto>> GetTransmissionTorrentsAsync(CancellationToken cancellationToken = default);
    Task<bool> RemoveTransmissionTorrentAsync(int torrentId, bool deleteData = true, CancellationToken cancellationToken = default);
    Task<bool> PauseTransmissionTorrentAsync(int torrentId, CancellationToken cancellationToken = default);
    Task<bool> ResumeTransmissionTorrentAsync(int torrentId, CancellationToken cancellationToken = default);
    Task<bool> KickstartTransmissionTorrentAsync(int torrentId, CancellationToken cancellationToken = default);
    string? ResolveCachedTitle(string? hashOrName);
}
