using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyTube.Backend.Application.Radarr;

namespace MyTube.Backend.Application.Features.Radarr.Commands;

public record SyncRadarrLinksCommand : IRequest<bool>;

public class SyncRadarrLinksCommandHandler : IRequestHandler<SyncRadarrLinksCommand, bool>
{
    private readonly IRadarrService _radarrService;

    public SyncRadarrLinksCommandHandler(IRadarrService radarrService)
    {
        _radarrService = radarrService;
    }

    public async Task<bool> Handle(SyncRadarrLinksCommand request, CancellationToken cancellationToken)
    {
        return await _radarrService.SyncRadarrMediaLinksAsync(cancellationToken);
    }
}
