using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyTube.Backend.Application.Radarr;

namespace MyTube.Backend.Application.Features.Radarr.Queries;

public record GetRadarrLibraryQuery : IRequest<List<RadarrMovieDto>>;

public class GetRadarrLibraryQueryHandler : IRequestHandler<GetRadarrLibraryQuery, List<RadarrMovieDto>>
{
    private readonly IRadarrService _radarrService;

    public GetRadarrLibraryQueryHandler(IRadarrService radarrService)
    {
        _radarrService = radarrService;
    }

    public async Task<List<RadarrMovieDto>> Handle(GetRadarrLibraryQuery request, CancellationToken cancellationToken)
    {
        return await _radarrService.GetLibraryMoviesAsync(cancellationToken);
    }
}
