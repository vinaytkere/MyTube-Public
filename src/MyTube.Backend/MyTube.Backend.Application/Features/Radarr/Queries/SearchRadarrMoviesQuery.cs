using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Radarr;

namespace MyTube.Backend.Application.Features.Radarr.Queries;

public record SearchRadarrMoviesQuery(string Query) : IRequest<List<RadarrMovieDto>>;

public class SearchRadarrMoviesQueryValidator : AbstractValidator<SearchRadarrMoviesQuery>
{
    public SearchRadarrMoviesQueryValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty().WithMessage("Query parameter is required.");
    }
}

public class SearchRadarrMoviesQueryHandler : IRequestHandler<SearchRadarrMoviesQuery, List<RadarrMovieDto>>
{
    private readonly IRadarrService _radarrService;

    public SearchRadarrMoviesQueryHandler(IRadarrService radarrService)
    {
        _radarrService = radarrService;
    }

    public async Task<List<RadarrMovieDto>> Handle(SearchRadarrMoviesQuery request, CancellationToken cancellationToken)
    {
        return await _radarrService.SearchMoviesAsync(request.Query, cancellationToken);
    }
}
