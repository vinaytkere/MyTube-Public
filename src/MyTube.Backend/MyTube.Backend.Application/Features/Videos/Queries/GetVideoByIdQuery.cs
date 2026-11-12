using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;

namespace MyTube.Backend.Application.Features.Videos.Queries;

public record GetVideoByIdQuery(Guid Id, string UserId, string BaseUrl) : IRequest<VideoDto?>;

public class GetVideoByIdQueryValidator : AbstractValidator<GetVideoByIdQuery>
{
    public GetVideoByIdQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Video ID cannot be empty.");
    }
}

public class GetVideoByIdQueryHandler : IRequestHandler<GetVideoByIdQuery, VideoDto?>
{
    private readonly IVideoService _videoService;

    public GetVideoByIdQueryHandler(IVideoService videoService)
    {
        _videoService = videoService;
    }

    public async Task<VideoDto?> Handle(GetVideoByIdQuery request, CancellationToken cancellationToken)
    {
        return await _videoService.GetVideoByIdAsync(request.Id, request.UserId, request.BaseUrl, cancellationToken);
    }
}
