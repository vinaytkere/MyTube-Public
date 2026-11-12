using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyTube.Backend.Application.Radarr;

namespace MyTube.Backend.Application.Features.Radarr.Queries;

public record GetRadarrRootFoldersQuery : IRequest<List<RadarrRootFolderDto>>;

public class GetRadarrRootFoldersQueryHandler : IRequestHandler<GetRadarrRootFoldersQuery, List<RadarrRootFolderDto>>
{
    private readonly IRadarrService _radarrService;

    public GetRadarrRootFoldersQueryHandler(IRadarrService radarrService)
    {
        _radarrService = radarrService;
    }

    public async Task<List<RadarrRootFolderDto>> Handle(GetRadarrRootFoldersQuery request, CancellationToken cancellationToken)
    {
        return await _radarrService.GetRootFoldersAsync(cancellationToken);
    }
}
