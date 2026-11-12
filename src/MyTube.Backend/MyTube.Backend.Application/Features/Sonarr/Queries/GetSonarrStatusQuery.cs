using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyTube.Backend.Application.Sonarr;

namespace MyTube.Backend.Application.Features.Sonarr.Queries;

public record GetSonarrStatusQuery : IRequest<SonarrStatusDto>;

public class GetSonarrStatusQueryHandler : IRequestHandler<GetSonarrStatusQuery, SonarrStatusDto>
{
    private readonly ISonarrService _sonarrService;

    public GetSonarrStatusQueryHandler(ISonarrService sonarrService)
    {
        _sonarrService = sonarrService;
    }

    public async Task<SonarrStatusDto> Handle(GetSonarrStatusQuery request, CancellationToken cancellationToken)
    {
        return await _sonarrService.GetStatusAsync(cancellationToken);
    }
}
