using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace MyTube.Backend.Application.Features.Downloads.Commands;

public record UpdateYtDlpBinaryCommand() : IRequest<Unit>;

public class UpdateYtDlpBinaryCommandHandler : IRequestHandler<UpdateYtDlpBinaryCommand, Unit>
{
    private readonly IDownloadManager _downloadManager;

    public UpdateYtDlpBinaryCommandHandler(IDownloadManager downloadManager)
    {
        _downloadManager = downloadManager;
    }

    public async Task<Unit> Handle(UpdateYtDlpBinaryCommand request, CancellationToken cancellationToken)
    {
        await _downloadManager.UpdateYtDlpAsync();
        return Unit.Value;
    }
}
