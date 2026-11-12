using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyTube.Backend.Application.Radarr;

namespace MyTube.Backend.Application.Features.Radarr.Queries;

public record GetRadarrStatusQuery : IRequest<RadarrStatusDto>;

public class GetRadarrStatusQueryHandler : IRequestHandler<GetRadarrStatusQuery, RadarrStatusDto>
{
    private readonly IRadarrService _radarrService;

    public GetRadarrStatusQueryHandler(IRadarrService radarrService)
    {
        _radarrService = radarrService;
    }

    public async Task<RadarrStatusDto> Handle(GetRadarrStatusQuery request, CancellationToken cancellationToken)
    {
        return await _radarrService.GetStatusAsync(cancellationToken);
    }
}
