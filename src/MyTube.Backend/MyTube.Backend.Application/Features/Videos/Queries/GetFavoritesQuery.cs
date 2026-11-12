using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace MyTube.Backend.Application.Features.Videos.Queries;

public record GetFavoritesQuery(string UserId, string BaseUrl) : IRequest<List<VideoDto>>;

public class GetFavoritesQueryHandler : IRequestHandler<GetFavoritesQuery, List<VideoDto>>
{
    private readonly IVideoService _videoService;

    public GetFavoritesQueryHandler(IVideoService videoService)
    {
        _videoService = videoService;
    }

    public async Task<List<VideoDto>> Handle(GetFavoritesQuery request, CancellationToken cancellationToken)
    {
        return await _videoService.GetFavoritesAsync(request.UserId, request.BaseUrl, cancellationToken);
    }
}
