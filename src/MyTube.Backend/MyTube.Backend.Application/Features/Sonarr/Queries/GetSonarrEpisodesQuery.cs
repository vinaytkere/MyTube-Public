using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Sonarr;

namespace MyTube.Backend.Application.Features.Sonarr.Queries;

public record GetSonarrEpisodesQuery(int SeriesId) : IRequest<List<SonarrEpisodeDto>>;

public class GetSonarrEpisodesQueryValidator : AbstractValidator<GetSonarrEpisodesQuery>
{
    public GetSonarrEpisodesQueryValidator()
    {
        RuleFor(x => x.SeriesId)
            .GreaterThan(0).WithMessage("Valid seriesId is required.");
    }
}

public class GetSonarrEpisodesQueryHandler : IRequestHandler<GetSonarrEpisodesQuery, List<SonarrEpisodeDto>>
{
    private readonly ISonarrService _sonarrService;

    public GetSonarrEpisodesQueryHandler(ISonarrService sonarrService)
    {
        _sonarrService = sonarrService;
    }

    public async Task<List<SonarrEpisodeDto>> Handle(GetSonarrEpisodesQuery request, CancellationToken cancellationToken)
    {
        return await _sonarrService.GetEpisodesAsync(request.SeriesId, cancellationToken);
    }
}
