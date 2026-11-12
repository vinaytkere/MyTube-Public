using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;

namespace MyTube.Backend.Application.Features.Downloads.Commands;

public record StartDownloadCommand(DownloadRequest Request) : IRequest<DownloadJobDto>;

public class StartDownloadCommandValidator : AbstractValidator<StartDownloadCommand>
{
    public StartDownloadCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull().WithMessage("Download request cannot be null.");

        RuleFor(x => x.Request.Url)
            .NotEmpty().WithMessage("URL is required.")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("A valid HTTP or HTTPS URL is required.");
    }
}

public class StartDownloadCommandHandler : IRequestHandler<StartDownloadCommand, DownloadJobDto>
{
    private readonly IDownloadManager _downloadManager;

    public StartDownloadCommandHandler(IDownloadManager downloadManager)
    {
        _downloadManager = downloadManager;
    }

    public async Task<DownloadJobDto> Handle(StartDownloadCommand request, CancellationToken cancellationToken)
    {
        return await _downloadManager.StartDownloadAsync(request.Request);
    }
}
