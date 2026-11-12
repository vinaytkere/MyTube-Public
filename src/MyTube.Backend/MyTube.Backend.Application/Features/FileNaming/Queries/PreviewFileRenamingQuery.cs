using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.FileNaming;

namespace MyTube.Backend.Application.Features.FileNaming.Queries;

public record PreviewFileRenamingQuery(FileRenamePreviewRequest Request) : IRequest<FileRenamePreviewResultDto>;

public class PreviewFileRenamingQueryValidator : AbstractValidator<PreviewFileRenamingQuery>
{
    public PreviewFileRenamingQueryValidator()
    {
        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request cannot be null.");
    }
}

public class PreviewFileRenamingQueryHandler : IRequestHandler<PreviewFileRenamingQuery, FileRenamePreviewResultDto>
{
    private readonly IMediaNamingService _namingService;

    public PreviewFileRenamingQueryHandler(IMediaNamingService namingService)
    {
        _namingService = namingService;
    }

    public async Task<FileRenamePreviewResultDto> Handle(PreviewFileRenamingQuery request, CancellationToken cancellationToken)
    {
        return await _namingService.PreviewRenamingAsync(request.Request, cancellationToken);
    }
}
