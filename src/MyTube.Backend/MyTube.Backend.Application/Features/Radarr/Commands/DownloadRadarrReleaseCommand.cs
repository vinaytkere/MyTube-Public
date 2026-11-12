using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Radarr;

namespace MyTube.Backend.Application.Features.Radarr.Commands;

public record DownloadRadarrReleaseCommand(RadarrDownloadReleaseRequest Request) : IRequest<bool>;

public class DownloadRadarrReleaseCommandValidator : AbstractValidator<DownloadRadarrReleaseCommand>
{
    public DownloadRadarrReleaseCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request body is required.");

        When(x => x.Request != null, () =>
        {
            RuleFor(x => x.Request.Guid)
                .NotEmpty().WithMessage("Release Guid is required.");
        });
    }
}

public class DownloadRadarrReleaseCommandHandler : IRequestHandler<DownloadRadarrReleaseCommand, bool>
{
    private readonly IRadarrService _radarrService;

    public DownloadRadarrReleaseCommandHandler(IRadarrService radarrService)
    {
        _radarrService = radarrService;
    }

    public async Task<bool> Handle(DownloadRadarrReleaseCommand request, CancellationToken cancellationToken)
    {
        return await _radarrService.DownloadReleaseAsync(request.Request, cancellationToken);
    }
}
