using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.FileNaming;

namespace MyTube.Backend.Application.Features.FileNaming.Commands;

public record ExecuteFileRenamingCommand(FileRenameExecuteRequest Request) : IRequest<FileRenameExecuteResultDto>;

public class ExecuteFileRenamingCommandValidator : AbstractValidator<ExecuteFileRenamingCommand>
{
    public ExecuteFileRenamingCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request cannot be null.");
    }
}

public class ExecuteFileRenamingCommandHandler : IRequestHandler<ExecuteFileRenamingCommand, FileRenameExecuteResultDto>
{
    private readonly IMediaNamingService _namingService;

    public ExecuteFileRenamingCommandHandler(IMediaNamingService namingService)
    {
        _namingService = namingService;
    }

    public async Task<FileRenameExecuteResultDto> Handle(ExecuteFileRenamingCommand request, CancellationToken cancellationToken)
    {
        return await _namingService.ExecuteRenamingAsync(request.Request, cancellationToken);
    }
}
