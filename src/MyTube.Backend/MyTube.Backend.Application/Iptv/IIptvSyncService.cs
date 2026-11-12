using System.Threading;
using System.Threading.Tasks;

namespace MyTube.Backend.Application.Iptv;

public interface IIptvSyncService
{
    Task<IptvSyncStatusDto> GetSyncStatusAsync(CancellationToken ct = default);
    Task<IptvSyncStatusDto> SyncCatalogAsync(bool force = false, CancellationToken ct = default);
}

