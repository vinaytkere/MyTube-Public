using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyTube.Backend.Application.Repositories;
using MyTube.Backend.Domain;

namespace MyTube.Backend.Application.Features.Videos.Queries;

public record GetVideoFileQuery(Guid Id) : IRequest<Video?>;

public class GetVideoFileQueryHandler : IRequestHandler<GetVideoFileQuery, Video?>
{
    private readonly IVideoRepository _videoRepository;

    public GetVideoFileQueryHandler(IVideoRepository videoRepository)
    {
        _videoRepository = videoRepository;
    }

    public async Task<Video?> Handle(GetVideoFileQuery request, CancellationToken cancellationToken)
    {
        return await _videoRepository.GetByIdAsync(request.Id, cancellationToken);
    }
}
