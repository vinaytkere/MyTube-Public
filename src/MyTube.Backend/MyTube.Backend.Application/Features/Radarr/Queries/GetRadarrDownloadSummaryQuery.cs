using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyTube.Backend.Application.Radarr;

namespace MyTube.Backend.Application.Features.Radarr.Queries;

public record GetRadarrDownloadSummaryQuery : IRequest<RadarrDownloadSummaryDto>;

public class GetRadarrDownloadSummaryQueryHandler : IRequestHandler<GetRadarrDownloadSummaryQuery, RadarrDownloadSummaryDto>
{
    private readonly IRadarrService _radarrService;

    public GetRadarrDownloadSummaryQueryHandler(IRadarrService radarrService)
    {
        _radarrService = radarrService;
    }

    public async Task<RadarrDownloadSummaryDto> Handle(GetRadarrDownloadSummaryQuery request, CancellationToken cancellationToken)
    {
        return await _radarrService.GetDownloadedMediaSummaryAsync(cancellationToken);
    }
}
