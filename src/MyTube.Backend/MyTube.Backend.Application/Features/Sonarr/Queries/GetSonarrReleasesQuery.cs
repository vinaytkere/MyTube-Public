using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Sonarr;

namespace MyTube.Backend.Application.Features.Sonarr.Queries;

public record GetSonarrReleasesQuery(int SeriesId, int? EpisodeId = null) : IRequest<List<SonarrReleaseDto>>;

public class GetSonarrReleasesQueryValidator : AbstractValidator<GetSonarrReleasesQuery>
{
    public GetSonarrReleasesQueryValidator()
    {
        RuleFor(x => x.SeriesId)
            .GreaterThan(0).WithMessage("Valid seriesId is required.");
    }
}

public class GetSonarrReleasesQueryHandler : IRequestHandler<GetSonarrReleasesQuery, List<SonarrReleaseDto>>
{
    private readonly ISonarrService _sonarrService;

    public GetSonarrReleasesQueryHandler(ISonarrService sonarrService)
    {
        _sonarrService = sonarrService;
    }

    public async Task<List<SonarrReleaseDto>> Handle(GetSonarrReleasesQuery request, CancellationToken cancellationToken)
    {
        return await _sonarrService.GetReleasesAsync(request.SeriesId, request.EpisodeId, cancellationToken);
    }
}
