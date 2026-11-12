using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Sonarr;

namespace MyTube.Backend.Application.Features.Sonarr.Queries;

public record GetSonarrSeriesByIdQuery(int SeriesId) : IRequest<SonarrSeriesDto?>;

public class GetSonarrSeriesByIdQueryValidator : AbstractValidator<GetSonarrSeriesByIdQuery>
{
    public GetSonarrSeriesByIdQueryValidator()
    {
        RuleFor(x => x.SeriesId)
            .GreaterThan(0).WithMessage("Valid seriesId is required.");
    }
}

public class GetSonarrSeriesByIdQueryHandler : IRequestHandler<GetSonarrSeriesByIdQuery, SonarrSeriesDto?>
{
    private readonly ISonarrService _sonarrService;

    public GetSonarrSeriesByIdQueryHandler(ISonarrService sonarrService)
    {
        _sonarrService = sonarrService;
    }

    public async Task<SonarrSeriesDto?> Handle(GetSonarrSeriesByIdQuery request, CancellationToken cancellationToken)
    {
        return await _sonarrService.GetSeriesByIdAsync(request.SeriesId, cancellationToken);
    }
}
