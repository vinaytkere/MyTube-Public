using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;

namespace MyTube.Backend.Application.Features.Videos.Commands;

public record RenameVideoCommand(Guid VideoId, string NewTitle) : IRequest<bool>;

public class RenameVideoCommandValidator : AbstractValidator<RenameVideoCommand>
{
    public RenameVideoCommandValidator()
    {
        RuleFor(x => x.VideoId)
            .NotEmpty().WithMessage("Video ID cannot be empty.");

        RuleFor(x => x.NewTitle)
            .NotEmpty().WithMessage("New title cannot be empty.")
            .MaximumLength(255).WithMessage("New title cannot exceed 255 characters.");
    }
}

public class RenameVideoCommandHandler : IRequestHandler<RenameVideoCommand, bool>
{
    private readonly IVideoService _videoService;

    public RenameVideoCommandHandler(IVideoService videoService)
    {
        _videoService = videoService;
    }

    public async Task<bool> Handle(RenameVideoCommand request, CancellationToken cancellationToken)
    {
        return await _videoService.RenameVideoAsync(request.VideoId, request.NewTitle, cancellationToken);
    }
}
