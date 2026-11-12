using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;

namespace MyTube.Backend.Application.Features.Videos.Commands;

public record UpdateVideosCategoryBatchCommand(List<Guid> VideoIds, int CategoryId) : IRequest<int>;

public class UpdateVideosCategoryBatchCommandValidator : AbstractValidator<UpdateVideosCategoryBatchCommand>
{
    public UpdateVideosCategoryBatchCommandValidator()
    {
        RuleFor(x => x.VideoIds)
            .NotEmpty().WithMessage("No video IDs provided.");

        RuleFor(x => x.CategoryId)
            .GreaterThan(0).WithMessage("Category ID must be greater than 0.");
    }
}

public class UpdateVideosCategoryBatchCommandHandler : IRequestHandler<UpdateVideosCategoryBatchCommand, int>
{
    private readonly IVideoService _videoService;

    public UpdateVideosCategoryBatchCommandHandler(IVideoService videoService)
    {
        _videoService = videoService;
    }

    public async Task<int> Handle(UpdateVideosCategoryBatchCommand request, CancellationToken cancellationToken)
    {
        return await _videoService.UpdateVideosCategoryAsync(request.VideoIds, request.CategoryId, cancellationToken);
    }
}
