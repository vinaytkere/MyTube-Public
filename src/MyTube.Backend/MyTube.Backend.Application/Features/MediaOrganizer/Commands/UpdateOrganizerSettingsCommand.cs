using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.MediaOrganizer;
using MyTube.Backend.Application.MediaOrganizer.Dtos;

namespace MyTube.Backend.Application.Features.MediaOrganizer.Commands;

public record UpdateOrganizerSettingsCommand(MediaOrganizerSettingsDto Settings) : IRequest<bool>;

public class UpdateOrganizerSettingsCommandValidator : AbstractValidator<UpdateOrganizerSettingsCommand>
{
    public UpdateOrganizerSettingsCommandValidator()
    {
        RuleFor(x => x.Settings)
            .NotNull().WithMessage("Settings cannot be null.");
        RuleFor(x => x.Settings.MovieFolderPattern)
            .NotEmpty().WithMessage("MovieFolderPattern cannot be empty.");
        RuleFor(x => x.Settings.AudioFolderPattern)
            .NotEmpty().WithMessage("AudioFolderPattern cannot be empty.");
    }
}

public class UpdateOrganizerSettingsCommandHandler : IRequestHandler<UpdateOrganizerSettingsCommand, bool>
{
    private readonly IMediaOrganizerService _organizerService;

    public UpdateOrganizerSettingsCommandHandler(IMediaOrganizerService organizerService)
    {
        _organizerService = organizerService;
    }

    public async Task<bool> Handle(UpdateOrganizerSettingsCommand request, CancellationToken cancellationToken)
    {
        await _organizerService.UpdateSettingsAsync(request.Settings, cancellationToken);
        return true;
    }
}
