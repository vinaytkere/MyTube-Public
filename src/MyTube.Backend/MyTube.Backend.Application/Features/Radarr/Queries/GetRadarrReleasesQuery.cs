using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Radarr;

namespace MyTube.Backend.Application.Features.Radarr.Queries;

public record GetRadarrReleasesQuery(int MovieId) : IRequest<List<RadarrReleaseDto>>;

public class GetRadarrReleasesQueryValidator : AbstractValidator<GetRadarrReleasesQuery>
{
    public GetRadarrReleasesQueryValidator()
    {
        RuleFor(x => x.MovieId)
            .GreaterThan(0).WithMessage("Valid movieId is required.");
    }
}

public class GetRadarrReleasesQueryHandler : IRequestHandler<GetRadarrReleasesQuery, List<RadarrReleaseDto>>
{
    private readonly IRadarrService _radarrService;

    public GetRadarrReleasesQueryHandler(IRadarrService radarrService)
    {
        _radarrService = radarrService;
    }

    public async Task<List<RadarrReleaseDto>> Handle(GetRadarrReleasesQuery request, CancellationToken cancellationToken)
    {
        return await _radarrService.GetReleasesAsync(request.MovieId, cancellationToken);
    }
}
