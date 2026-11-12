using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyTube.Backend.Application.Sonarr;

namespace MyTube.Backend.Application.Features.Sonarr.Queries;

public record GetSonarrRootFoldersQuery : IRequest<List<SonarrRootFolderDto>>;

public class GetSonarrRootFoldersQueryHandler : IRequestHandler<GetSonarrRootFoldersQuery, List<SonarrRootFolderDto>>
{
    private readonly ISonarrService _sonarrService;

    public GetSonarrRootFoldersQueryHandler(ISonarrService sonarrService)
    {
        _sonarrService = sonarrService;
    }

    public async Task<List<SonarrRootFolderDto>> Handle(GetSonarrRootFoldersQuery request, CancellationToken cancellationToken)
    {
        return await _sonarrService.GetRootFoldersAsync(cancellationToken);
    }
}
