using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Sonarr;

namespace MyTube.Backend.Application.Features.Sonarr.Commands;

public record UpdateSonarrSettingsCommand(SonarrSettings Settings) : IRequest<bool>;

public class UpdateSonarrSettingsCommandValidator : AbstractValidator<UpdateSonarrSettingsCommand>
{
    public UpdateSonarrSettingsCommandValidator()
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

public class UpdateSonarrSettingsCommandHandler : IRequestHandler<UpdateSonarrSettingsCommand, bool>
{
    private readonly ISonarrService _sonarrService;

    public UpdateSonarrSettingsCommandHandler(ISonarrService sonarrService)
    {
        _sonarrService = sonarrService;
    }

    public async Task<bool> Handle(UpdateSonarrSettingsCommand request, CancellationToken cancellationToken)
    {
        await _sonarrService.UpdateSettingsAsync(request.Settings, cancellationToken);
        return true;
    }
}
