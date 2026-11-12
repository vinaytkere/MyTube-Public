using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace MyTube.Backend.Application.Features.Downloads.Commands;

public record CancelAllDownloadsCommand() : IRequest<int>;

public class CancelAllDownloadsCommandHandler : IRequestHandler<CancelAllDownloadsCommand, int>
{
    private readonly IDownloadManager _downloadManager;

    public CancelAllDownloadsCommandHandler(IDownloadManager downloadManager)
    {
        _downloadManager = downloadManager;
    }

    public async Task<int> Handle(CancelAllDownloadsCommand request, CancellationToken cancellationToken)
    {
        return await _downloadManager.CancelAllAsync(cancellationToken);
    }
}
