using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Sonarr;

namespace MyTube.Backend.Application.Features.Sonarr.Commands;

public record DownloadSonarrReleaseCommand(SonarrDownloadReleaseRequest Request) : IRequest<bool>;

public class DownloadSonarrReleaseCommandValidator : AbstractValidator<DownloadSonarrReleaseCommand>
{
    public DownloadSonarrReleaseCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request cannot be null.");

        When(x => x.Request != null, () =>
        {
            RuleFor(x => x.Request.Guid)
                .NotEmpty().WithMessage("Release Guid is required.");
        });
    }
}

public class DownloadSonarrReleaseCommandHandler : IRequestHandler<DownloadSonarrReleaseCommand, bool>
{
    private readonly ISonarrService _sonarrService;

    public DownloadSonarrReleaseCommandHandler(ISonarrService sonarrService)
    {
        _sonarrService = sonarrService;
    }

    public async Task<bool> Handle(DownloadSonarrReleaseCommand request, CancellationToken cancellationToken)
    {
        return await _sonarrService.DownloadReleaseAsync(request.Request, cancellationToken);
    }
}
