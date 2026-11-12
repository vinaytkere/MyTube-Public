using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyTube.Backend.Application.Sonarr;

namespace MyTube.Backend.Application.Features.Sonarr.Queries;

public record GetSonarrQualityProfilesQuery : IRequest<List<SonarrQualityProfileDto>>;

public class GetSonarrQualityProfilesQueryHandler : IRequestHandler<GetSonarrQualityProfilesQuery, List<SonarrQualityProfileDto>>
{
    private readonly ISonarrService _sonarrService;

    public GetSonarrQualityProfilesQueryHandler(ISonarrService sonarrService)
    {
        _sonarrService = sonarrService;
    }

    public async Task<List<SonarrQualityProfileDto>> Handle(GetSonarrQualityProfilesQuery request, CancellationToken cancellationToken)
    {
        return await _sonarrService.GetQualityProfilesAsync(cancellationToken);
    }
}
