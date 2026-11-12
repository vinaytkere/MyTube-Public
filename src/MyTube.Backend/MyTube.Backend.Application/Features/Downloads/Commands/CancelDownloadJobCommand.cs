using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;

namespace MyTube.Backend.Application.Features.Downloads.Commands;

public record CancelDownloadJobCommand(Guid JobId) : IRequest<bool>;

public class CancelDownloadJobCommandValidator : AbstractValidator<CancelDownloadJobCommand>
{
    public CancelDownloadJobCommandValidator()
    {
        RuleFor(x => x.JobId)
            .NotEmpty().WithMessage("Job ID cannot be empty.");
    }
}

public class CancelDownloadJobCommandHandler : IRequestHandler<CancelDownloadJobCommand, bool>
{
    private readonly IDownloadManager _downloadManager;

    public CancelDownloadJobCommandHandler(IDownloadManager downloadManager)
    {
        _downloadManager = downloadManager;
    }

    public Task<bool> Handle(CancelDownloadJobCommand request, CancellationToken cancellationToken)
    {
        var result = _downloadManager.CancelJob(request.JobId);
        return Task.FromResult(result);
    }
}
