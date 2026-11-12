using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Sonarr;

namespace MyTube.Backend.Application.Features.Sonarr.Queries;

public record SearchSonarrSeriesQuery(string Query) : IRequest<List<SonarrSeriesDto>>;

public class SearchSonarrSeriesQueryValidator : AbstractValidator<SearchSonarrSeriesQuery>
{
    public SearchSonarrSeriesQueryValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty().WithMessage("Query parameter is required.");
    }
}

public class SearchSonarrSeriesQueryHandler : IRequestHandler<SearchSonarrSeriesQuery, List<SonarrSeriesDto>>
{
    private readonly ISonarrService _sonarrService;

    public SearchSonarrSeriesQueryHandler(ISonarrService sonarrService)
    {
        _sonarrService = sonarrService;
    }

    public async Task<List<SonarrSeriesDto>> Handle(SearchSonarrSeriesQuery request, CancellationToken cancellationToken)
    {
        return await _sonarrService.SearchSeriesAsync(request.Query, cancellationToken);
    }
}
