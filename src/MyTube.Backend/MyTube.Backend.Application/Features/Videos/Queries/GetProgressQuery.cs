using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyTube.Backend.Application.Repositories;

namespace MyTube.Backend.Application.Features.Videos.Queries;

public record GetProgressQuery(string UserId, Guid VideoId) : IRequest<WatchProgressDto?>;

public class GetProgressQueryHandler : IRequestHandler<GetProgressQuery, WatchProgressDto?>
{
    private readonly IVideoRepository _videoRepository;

    public GetProgressQueryHandler(IVideoRepository videoRepository)
    {
        _videoRepository = videoRepository;
    }

    public async Task<WatchProgressDto?> Handle(GetProgressQuery request, CancellationToken cancellationToken)
    {
        var progress = await _videoRepository.GetUserProgressAsync(request.UserId, request.VideoId, cancellationToken);
        if (progress == null) return null;

        return new WatchProgressDto
        {
            VideoId = progress.VideoId,
            PositionSeconds = progress.PositionSeconds,
            DurationSeconds = progress.DurationSeconds,
            Completed = progress.Completed,
            UpdatedAt = progress.UpdatedAt
        };
    }
}
