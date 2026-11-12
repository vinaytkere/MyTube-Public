using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyTube.Backend.Application.Iptv;

namespace MyTube.Backend.Application.Features.Iptv.Queries;

public record GetIptvSyncStatusQuery : IRequest<IptvSyncStatusDto>;

public class GetIptvSyncStatusQueryHandler : IRequestHandler<GetIptvSyncStatusQuery, IptvSyncStatusDto>
{
    private readonly IIptvSyncService _syncService;

    public GetIptvSyncStatusQueryHandler(IIptvSyncService syncService)
    {
        _syncService = syncService;
    }

    public async Task<IptvSyncStatusDto> Handle(GetIptvSyncStatusQuery request, CancellationToken cancellationToken)
    {
        return await _syncService.GetSyncStatusAsync(cancellationToken);
    }
}

