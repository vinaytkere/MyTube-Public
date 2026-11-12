using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Radarr;

namespace MyTube.Backend.Application.Features.Radarr.Commands;

public record UpdateRadarrSettingsCommand(RadarrSettings Settings) : IRequest<bool>;

public class UpdateRadarrSettingsCommandValidator : AbstractValidator<UpdateRadarrSettingsCommand>
{
    public UpdateRadarrSettingsCommandValidator()
    {
        RuleFor(x => x.Settings)
            .NotNull().WithMessage("Settings cannot be null.");

        When(x => x.Settings != null, () =>
        {
            RuleFor(x => x.Settings.BaseUrl)
                .NotEmpty().WithMessage("Base URL is required.");
        });
    }
}

public class UpdateRadarrSettingsCommandHandler : IRequestHandler<UpdateRadarrSettingsCommand, bool>
{
    private readonly IRadarrService _radarrService;

    public UpdateRadarrSettingsCommandHandler(IRadarrService radarrService)
    {
        _radarrService = radarrService;
    }

    public async Task<bool> Handle(UpdateRadarrSettingsCommand request, CancellationToken cancellationToken)
    {
        await _radarrService.UpdateSettingsAsync(request.Settings, cancellationToken);
        return true;
    }
}
