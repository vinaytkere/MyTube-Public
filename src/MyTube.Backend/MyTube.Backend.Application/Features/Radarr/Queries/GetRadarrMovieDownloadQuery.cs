using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Radarr;

namespace MyTube.Backend.Application.Features.Radarr.Queries;

public record GetRadarrMovieDownloadQuery(int MovieId) : IRequest<RadarrMovieDownloadResult?>;

public class GetRadarrMovieDownloadQueryValidator : AbstractValidator<GetRadarrMovieDownloadQuery>
{
    public GetRadarrMovieDownloadQueryValidator()
    {
        RuleFor(x => x.MovieId)
            .GreaterThan(0).WithMessage("Valid movieId is required.");
    }
}

public class GetRadarrMovieDownloadQueryHandler : IRequestHandler<GetRadarrMovieDownloadQuery, RadarrMovieDownloadResult?>
{
    private readonly IRadarrService _radarrService;

    public GetRadarrMovieDownloadQueryHandler(IRadarrService radarrService)
    {
        _radarrService = radarrService;
    }

    public async Task<RadarrMovieDownloadResult?> Handle(GetRadarrMovieDownloadQuery request, CancellationToken cancellationToken)
    {
        var result = await _radarrService.GetMovieDownloadFileAsync(request.MovieId, cancellationToken);
        if (result == null) return null;

        return new RadarrMovieDownloadResult(result.Value.FilePath, result.Value.ContentType, result.Value.FileName);
    }
}
