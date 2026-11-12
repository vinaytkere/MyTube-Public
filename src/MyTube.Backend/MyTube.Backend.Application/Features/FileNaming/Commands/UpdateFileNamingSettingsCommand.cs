using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.FileNaming;

namespace MyTube.Backend.Application.Features.FileNaming.Commands;

public record UpdateFileNamingSettingsCommand(FileNamingSettingsDto Settings) : IRequest<bool>;

public class UpdateFileNamingSettingsCommandValidator : AbstractValidator<UpdateFileNamingSettingsCommand>
{
    public UpdateFileNamingSettingsCommandValidator()
    {
        RuleFor(x => x.Settings)
            .NotNull().WithMessage("Settings cannot be null.");

        RuleFor(x => x.Settings.Pattern)
            .NotEmpty().WithMessage("Pattern cannot be empty.");
    }
}

public class UpdateFileNamingSettingsCommandHandler : IRequestHandler<UpdateFileNamingSettingsCommand, bool>
{
    private readonly IMediaNamingService _namingService;

    public UpdateFileNamingSettingsCommandHandler(IMediaNamingService namingService)
    {
        _namingService = namingService;
    }

    public async Task<bool> Handle(UpdateFileNamingSettingsCommand request, CancellationToken cancellationToken)
    {
        await _namingService.UpdateSettingsAsync(request.Settings, cancellationToken);
        return true;
    }
}
