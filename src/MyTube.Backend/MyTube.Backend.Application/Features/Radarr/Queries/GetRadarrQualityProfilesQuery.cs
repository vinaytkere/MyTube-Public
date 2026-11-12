using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyTube.Backend.Application.Radarr;

namespace MyTube.Backend.Application.Features.Radarr.Queries;

public record GetRadarrQualityProfilesQuery : IRequest<List<RadarrQualityProfileDto>>;

public class GetRadarrQualityProfilesQueryHandler : IRequestHandler<GetRadarrQualityProfilesQuery, List<RadarrQualityProfileDto>>
{
    private readonly IRadarrService _radarrService;

    public GetRadarrQualityProfilesQueryHandler(IRadarrService radarrService)
    {
        _radarrService = radarrService;
    }

    public async Task<List<RadarrQualityProfileDto>> Handle(GetRadarrQualityProfilesQuery request, CancellationToken cancellationToken)
    {
        return await _radarrService.GetQualityProfilesAsync(cancellationToken);
    }
}
