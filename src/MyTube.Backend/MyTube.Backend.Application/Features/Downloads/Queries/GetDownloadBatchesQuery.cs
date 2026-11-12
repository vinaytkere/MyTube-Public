using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace MyTube.Backend.Application.Features.Downloads.Queries;

public record GetDownloadBatchesQuery() : IRequest<List<DownloadBatchDto>>;

public class GetDownloadBatchesQueryHandler : IRequestHandler<GetDownloadBatchesQuery, List<DownloadBatchDto>>
{
    private readonly IDownloadManager _downloadManager;

    public GetDownloadBatchesQueryHandler(IDownloadManager downloadManager)
    {
        _downloadManager = downloadManager;
    }

    public async Task<List<DownloadBatchDto>> Handle(GetDownloadBatchesQuery request, CancellationToken cancellationToken)
    {
        return await _downloadManager.GetBatchesAsync(cancellationToken);
    }
}
