using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Radarr;

namespace MyTube.Backend.Application.Features.Radarr.Queries;

public record GetRadarrPosterDownloadQuery(int MovieId) : IRequest<RadarrMediaImageResult?>;

public class GetRadarrPosterDownloadQueryValidator : AbstractValidator<GetRadarrPosterDownloadQuery>
{
    public GetRadarrPosterDownloadQueryValidator()
    {
        RuleFor(x => x.MovieId)
            .GreaterThan(0).WithMessage("Valid movieId is required.");
    }
}

public class GetRadarrPosterDownloadQueryHandler : IRequestHandler<GetRadarrPosterDownloadQuery, RadarrMediaImageResult?>
{
    private readonly IRadarrService _radarrService;

    public GetRadarrPosterDownloadQueryHandler(IRadarrService radarrService)
    {
        _radarrService = radarrService;
    }

    public async Task<RadarrMediaImageResult?> Handle(GetRadarrPosterDownloadQuery request, CancellationToken cancellationToken)
    {
        var poster = await _radarrService.GetMoviePosterDownloadAsync(request.MovieId, cancellationToken);
        if (poster == null) return null;

        return new RadarrMediaImageResult(poster.Value.ImageBytes, poster.Value.ContentType, poster.Value.FileName);
    }
}

public record GetRadarrFanartDownloadQuery(int MovieId) : IRequest<RadarrMediaImageResult?>;

public class GetRadarrFanartDownloadQueryValidator : AbstractValidator<GetRadarrFanartDownloadQuery>
{
    public GetRadarrFanartDownloadQueryValidator()
    {
        RuleFor(x => x.MovieId)
            .GreaterThan(0).WithMessage("Valid movieId is required.");
    }
}

public class GetRadarrFanartDownloadQueryHandler : IRequestHandler<GetRadarrFanartDownloadQuery, RadarrMediaImageResult?>
{
    private readonly IRadarrService _radarrService;

    public GetRadarrFanartDownloadQueryHandler(IRadarrService radarrService)
    {
        _radarrService = radarrService;
    }

    public async Task<RadarrMediaImageResult?> Handle(GetRadarrFanartDownloadQuery request, CancellationToken cancellationToken)
    {
        var fanart = await _radarrService.GetMovieFanartDownloadAsync(request.MovieId, cancellationToken);
        if (fanart == null) return null;

        return new RadarrMediaImageResult(fanart.Value.ImageBytes, fanart.Value.ContentType, fanart.Value.FileName);
    }
}
