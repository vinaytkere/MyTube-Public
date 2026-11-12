using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;

namespace MyTube.Backend.Application.Features.Videos.Queries;

public record GetContinueWatchingQuery(string UserId, string BaseUrl, int Limit = 6) : IRequest<List<VideoDto>>;

public class GetContinueWatchingQueryValidator : AbstractValidator<GetContinueWatchingQuery>
{
    public GetContinueWatchingQueryValidator()
    {
        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 50).WithMessage("Limit must be between 1 and 50.");
    }
}

public class GetContinueWatchingQueryHandler : IRequestHandler<GetContinueWatchingQuery, List<VideoDto>>
{
    private readonly IVideoService _videoService;

    public GetContinueWatchingQueryHandler(IVideoService videoService)
    {
        _videoService = videoService;
    }

    public async Task<List<VideoDto>> Handle(GetContinueWatchingQuery request, CancellationToken cancellationToken)
    {
        return await _videoService.GetContinueWatchingAsync(request.UserId, request.BaseUrl, request.Limit, cancellationToken);
    }
}
