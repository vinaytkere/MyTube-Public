using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;

namespace MyTube.Backend.Application.Features.Downloads.Commands;

public record DeleteBatchCommand(Guid BatchId) : IRequest<bool>;

public class DeleteBatchCommandValidator : AbstractValidator<DeleteBatchCommand>
{
    public DeleteBatchCommandValidator()
    {
        RuleFor(x => x.BatchId).NotEmpty().WithMessage("Batch ID cannot be empty.");
    }
}

public class DeleteBatchCommandHandler : IRequestHandler<DeleteBatchCommand, bool>
{
    private readonly IDownloadManager _downloadManager;

    public DeleteBatchCommandHandler(IDownloadManager downloadManager)
    {
        _downloadManager = downloadManager;
    }

    public async Task<bool> Handle(DeleteBatchCommand request, CancellationToken cancellationToken)
    {
        return await _downloadManager.DeleteBatchAsync(request.BatchId, cancellationToken);
    }
}
