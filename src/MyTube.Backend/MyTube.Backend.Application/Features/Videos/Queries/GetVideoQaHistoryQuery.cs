using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyTube.Backend.Application.Repositories;

namespace MyTube.Backend.Application.Features.Videos.Queries;

public record GetVideoQaHistoryQuery(Guid VideoId) : IRequest<IReadOnlyList<VideoQaDto>>;

public class GetVideoQaHistoryQueryHandler : IRequestHandler<GetVideoQaHistoryQuery, IReadOnlyList<VideoQaDto>>
{
    private readonly IVideoRepository _videoRepository;

    public GetVideoQaHistoryQueryHandler(IVideoRepository videoRepository)
    {
        _videoRepository = videoRepository;
    }

    public async Task<IReadOnlyList<VideoQaDto>> Handle(GetVideoQaHistoryQuery request, CancellationToken cancellationToken)
    {
        var qas = await _videoRepository.GetQaHistoryAsync(request.VideoId, cancellationToken);

        return qas.Select(q => new VideoQaDto
        {
            Id = q.Id,
            VideoId = q.VideoId,
            UserId = q.UserId,
            Question = q.Question,
            Answer = q.Answer,
            FirecrawlContext = q.FirecrawlContext,
            CreatedAt = q.CreatedAt
        }).ToList();
    }
}
