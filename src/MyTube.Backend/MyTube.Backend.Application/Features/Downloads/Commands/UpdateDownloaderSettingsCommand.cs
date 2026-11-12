using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;

namespace MyTube.Backend.Application.Features.Downloads.Commands;

public record UpdateDownloaderSettingsCommand(DownloaderSettingsDto Settings) : IRequest<bool>;

public class UpdateDownloaderSettingsCommandValidator : AbstractValidator<UpdateDownloaderSettingsCommand>
{
    public UpdateDownloaderSettingsCommandValidator()
    {
        RuleFor(x => x.Settings)
            .NotNull().WithMessage("Settings cannot be null.");
    }
}

public class UpdateDownloaderSettingsCommandHandler : IRequestHandler<UpdateDownloaderSettingsCommand, bool>
{
    private readonly IDownloaderSettingsStore _settingsStore;

    public UpdateDownloaderSettingsCommandHandler(IDownloaderSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
    }

    public async Task<bool> Handle(UpdateDownloaderSettingsCommand request, CancellationToken cancellationToken)
    {
        await _settingsStore.UpdateSettingsAsync(request.Settings, cancellationToken);
        return true;
    }
}
