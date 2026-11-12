using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyTube.Backend.Application.Radarr;

namespace MyTube.Backend.Application.Features.Radarr.Queries;

public record GetRadarrQueueQuery : IRequest<List<RadarrQueueItemDto>>;

public class GetRadarrQueueQueryHandler : IRequestHandler<GetRadarrQueueQuery, List<RadarrQueueItemDto>>
{
    private readonly IRadarrService _radarrService;

    public GetRadarrQueueQueryHandler(IRadarrService radarrService)
    {
        _radarrService = radarrService;
    }

    public async Task<List<RadarrQueueItemDto>> Handle(GetRadarrQueueQuery request, CancellationToken cancellationToken)
    {
        return await _radarrService.GetQueueAsync(cancellationToken);
    }
}
