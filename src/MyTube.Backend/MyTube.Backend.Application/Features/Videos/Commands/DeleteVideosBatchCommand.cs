using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;

namespace MyTube.Backend.Application.Features.Videos.Commands;

public record DeleteVideosBatchCommand(List<Guid> VideoIds) : IRequest<int>;

public class DeleteVideosBatchCommandValidator : AbstractValidator<DeleteVideosBatchCommand>
{
    public DeleteVideosBatchCommandValidator()
    {
        RuleFor(x => x.VideoIds)
            .NotEmpty().WithMessage("No video IDs provided.");
    }
}

public class DeleteVideosBatchCommandHandler : IRequestHandler<DeleteVideosBatchCommand, int>
{
    private readonly IVideoService _videoService;

    public DeleteVideosBatchCommandHandler(IVideoService videoService)
    {
        _videoService = videoService;
    }

    public async Task<int> Handle(DeleteVideosBatchCommand request, CancellationToken cancellationToken)
    {
        return await _videoService.DeleteVideosAsync(request.VideoIds, cancellationToken);
    }
}
