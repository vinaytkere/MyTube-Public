using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;

namespace MyTube.Backend.Application.Features.Downloads.Commands;

public record StartBatchDownloadCommand(BatchDownloadRequest Request) : IRequest<BatchDownloadResponseDto>;

public class StartBatchDownloadCommandValidator : AbstractValidator<StartBatchDownloadCommand>
{
    private const int MaxBatchUrls = 100;

    public StartBatchDownloadCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull().WithMessage("Batch download request cannot be null.");

        When(x => x.Request != null, () =>
        {
            RuleFor(x => x.Request.Urls)
                .NotNull().WithMessage("URLs collection cannot be null.")
                .Must(urls => urls != null && urls.Count > 0).WithMessage("At least one URL must be provided.")
                .Must(urls => urls != null && urls.Count <= MaxBatchUrls)
                .WithMessage($"A batch cannot exceed {MaxBatchUrls} URLs at a time.");

            RuleForEach(x => x.Request.Urls)
                .NotEmpty().WithMessage("URL cannot be empty.")
                .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                             (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                .WithMessage("A valid HTTP or HTTPS URL is required.");
        });
    }
}

public class StartBatchDownloadCommandHandler : IRequestHandler<StartBatchDownloadCommand, BatchDownloadResponseDto>
{
    private readonly IDownloadManager _downloadManager;

    public StartBatchDownloadCommandHandler(IDownloadManager downloadManager)
    {
        _downloadManager = downloadManager;
    }

    public async Task<BatchDownloadResponseDto> Handle(StartBatchDownloadCommand request, CancellationToken cancellationToken)
    {
        return await _downloadManager.StartBatchDownloadAsync(request.Request, cancellationToken);
    }
}
