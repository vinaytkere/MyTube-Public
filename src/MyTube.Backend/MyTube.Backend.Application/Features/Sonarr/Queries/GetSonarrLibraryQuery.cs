using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyTube.Backend.Application.Sonarr;

namespace MyTube.Backend.Application.Features.Sonarr.Queries;

public record GetSonarrLibraryQuery : IRequest<List<SonarrSeriesDto>>;

public class GetSonarrLibraryQueryHandler : IRequestHandler<GetSonarrLibraryQuery, List<SonarrSeriesDto>>
{
    private readonly ISonarrService _sonarrService;

    public GetSonarrLibraryQueryHandler(ISonarrService sonarrService)
    {
        _sonarrService = sonarrService;
    }

    public async Task<List<SonarrSeriesDto>> Handle(GetSonarrLibraryQuery request, CancellationToken cancellationToken)
    {
        return await _sonarrService.GetLibrarySeriesAsync(cancellationToken);
    }
}
