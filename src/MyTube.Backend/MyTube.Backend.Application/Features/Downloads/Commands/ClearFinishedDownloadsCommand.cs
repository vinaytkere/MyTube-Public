using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace MyTube.Backend.Application.Features.Downloads.Commands;

public record ClearFinishedDownloadsCommand() : IRequest<int>;

public class ClearFinishedDownloadsCommandHandler : IRequestHandler<ClearFinishedDownloadsCommand, int>
{
    private readonly IDownloadManager _downloadManager;

    public ClearFinishedDownloadsCommandHandler(IDownloadManager downloadManager)
    {
        _downloadManager = downloadManager;
    }

    public async Task<int> Handle(ClearFinishedDownloadsCommand request, CancellationToken cancellationToken)
    {
        return await _downloadManager.ClearFinishedJobsAsync(cancellationToken);
    }
}
