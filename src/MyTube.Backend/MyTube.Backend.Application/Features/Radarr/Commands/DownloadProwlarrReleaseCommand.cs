using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Prowlarr;

namespace MyTube.Backend.Application.Features.Radarr.Commands;

public record DownloadProwlarrReleaseCommand(ProwlarrDownloadRequest Request) : IRequest<bool>;

public class DownloadProwlarrReleaseCommandValidator : AbstractValidator<DownloadProwlarrReleaseCommand>
{
    public DownloadProwlarrReleaseCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request cannot be null.");

        When(x => x.Request != null, () =>
        {
            RuleFor(x => x.Request)
                .Must(r => !string.IsNullOrWhiteSpace(r.Guid) || !string.IsNullOrWhiteSpace(r.DownloadUrl) || !string.IsNullOrWhiteSpace(r.MagnetUrl))
                .WithMessage("Valid torrent link, magnet URL, or GUID is required.");
        });
    }
}

public class DownloadProwlarrReleaseCommandHandler : IRequestHandler<DownloadProwlarrReleaseCommand, bool>
{
    private readonly IProwlarrService _prowlarrService;

    public DownloadProwlarrReleaseCommandHandler(IProwlarrService prowlarrService)
    {
        _prowlarrService = prowlarrService;
    }

    public async Task<bool> Handle(DownloadProwlarrReleaseCommand request, CancellationToken cancellationToken)
    {
        return await _prowlarrService.DownloadAsync(request.Request, cancellationToken);
    }
}
