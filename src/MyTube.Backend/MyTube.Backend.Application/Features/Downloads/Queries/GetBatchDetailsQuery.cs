using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace MyTube.Backend.Application.Features.Downloads.Queries;

public record GetBatchDetailsQuery(Guid BatchId) : IRequest<DownloadBatchDto?>;

public class GetBatchDetailsQueryHandler : IRequestHandler<GetBatchDetailsQuery, DownloadBatchDto?>
{
    private readonly IDownloadManager _downloadManager;

    public GetBatchDetailsQueryHandler(IDownloadManager downloadManager)
    {
        _downloadManager = downloadManager;
    }

    public async Task<DownloadBatchDto?> Handle(GetBatchDetailsQuery request, CancellationToken cancellationToken)
    {
        return await _downloadManager.GetBatchByIdAsync(request.BatchId, cancellationToken);
    }
}
