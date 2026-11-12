using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;

namespace MyTube.Backend.Application.Features.Videos.Commands;

public record UpdateVideoCategoryCommand(Guid VideoId, int CategoryId) : IRequest<bool>;

public class UpdateVideoCategoryCommandValidator : AbstractValidator<UpdateVideoCategoryCommand>
{
    public UpdateVideoCategoryCommandValidator()
    {
        RuleFor(x => x.VideoId)
            .NotEmpty().WithMessage("Video ID cannot be empty.");

        RuleFor(x => x.CategoryId)
            .GreaterThan(0).WithMessage("Category ID must be greater than 0.");
    }
}

public class UpdateVideoCategoryCommandHandler : IRequestHandler<UpdateVideoCategoryCommand, bool>
{
    private readonly IVideoService _videoService;

    public UpdateVideoCategoryCommandHandler(IVideoService videoService)
    {
        _videoService = videoService;
    }

    public async Task<bool> Handle(UpdateVideoCategoryCommand request, CancellationToken cancellationToken)
    {
        return await _videoService.UpdateVideoCategoryAsync(request.VideoId, request.CategoryId, cancellationToken);
    }
}
