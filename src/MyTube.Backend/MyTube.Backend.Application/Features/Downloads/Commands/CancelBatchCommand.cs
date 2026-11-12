using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;

namespace MyTube.Backend.Application.Features.Downloads.Commands;

public record CancelBatchCommand(Guid BatchId) : IRequest<bool>;

public class CancelBatchCommandValidator : AbstractValidator<CancelBatchCommand>
{
    public CancelBatchCommandValidator()
    {
        RuleFor(x => x.BatchId).NotEmpty().WithMessage("Batch ID cannot be empty.");
    }
}

public class CancelBatchCommandHandler : IRequestHandler<CancelBatchCommand, bool>
{
    private readonly IDownloadManager _downloadManager;

    public CancelBatchCommandHandler(IDownloadManager downloadManager)
    {
        _downloadManager = downloadManager;
    }

    public async Task<bool> Handle(CancelBatchCommand request, CancellationToken cancellationToken)
    {
        return await _downloadManager.CancelBatchAsync(request.BatchId, cancellationToken);
    }
}
