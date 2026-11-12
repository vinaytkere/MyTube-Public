using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.MediaOrganizer;
using MyTube.Backend.Application.MediaOrganizer.Dtos;

namespace MyTube.Backend.Application.Features.MediaOrganizer.Queries;

public record PreviewMediaRenamingQuery(Guid VideoId) : IRequest<MediaRenamePreviewDto>;

public class PreviewMediaRenamingQueryValidator : AbstractValidator<PreviewMediaRenamingQuery>
{
    public PreviewMediaRenamingQueryValidator()
    {
        RuleFor(x => x.VideoId)
            .NotEmpty().WithMessage("VideoId must be provided.");
    }
}

public class PreviewMediaRenamingQueryHandler : IRequestHandler<PreviewMediaRenamingQuery, MediaRenamePreviewDto>
{
    private readonly IMediaOrganizerService _organizerService;

    public PreviewMediaRenamingQueryHandler(IMediaOrganizerService organizerService)
    {
        _organizerService = organizerService;
    }

    public Task<MediaRenamePreviewDto> Handle(PreviewMediaRenamingQuery request, CancellationToken cancellationToken)
    {
        return _organizerService.PreviewOrganizationAsync(request.VideoId, cancellationToken);
    }
}
