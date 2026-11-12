using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyTube.Backend.Application.Sonarr;

namespace MyTube.Backend.Application.Features.Sonarr.Queries;

public record GetSonarrQueueQuery : IRequest<List<SonarrQueueItemDto>>;

public class GetSonarrQueueQueryHandler : IRequestHandler<GetSonarrQueueQuery, List<SonarrQueueItemDto>>
{
    private readonly ISonarrService _sonarrService;

    public GetSonarrQueueQueryHandler(ISonarrService sonarrService)
    {
        _sonarrService = sonarrService;
    }

    public async Task<List<SonarrQueueItemDto>> Handle(GetSonarrQueueQuery request, CancellationToken cancellationToken)
    {
        return await _sonarrService.GetQueueAsync(cancellationToken);
    }
}
