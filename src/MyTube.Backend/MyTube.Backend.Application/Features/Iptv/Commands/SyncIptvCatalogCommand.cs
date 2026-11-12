using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyTube.Backend.Application.Iptv;

namespace MyTube.Backend.Application.Features.Iptv.Commands;

public record SyncIptvCatalogCommand(bool Force = false) : IRequest<IptvSyncStatusDto>;

public class SyncIptvCatalogCommandHandler : IRequestHandler<SyncIptvCatalogCommand, IptvSyncStatusDto>
{
    private readonly IIptvSyncService _syncService;

    public SyncIptvCatalogCommandHandler(IIptvSyncService syncService)
    {
        _syncService = syncService;
    }

    public async Task<IptvSyncStatusDto> Handle(SyncIptvCatalogCommand request, CancellationToken cancellationToken)
    {
        return await _syncService.SyncCatalogAsync(request.Force, cancellationToken);
    }
}

