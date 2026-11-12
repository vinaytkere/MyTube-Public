using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;

namespace MyTube.Backend.Application.Features.Videos.Queries;

public record GetVideosQuery(
    string UserId,
    string BaseUrl,
    string? Search,
    int? CategoryId,
    string? SortBy,
    string? MediaType,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<VideoDto>>;

public class GetVideosQueryValidator : AbstractValidator<GetVideosQuery>
{
    public GetVideosQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");
    }
}

public class GetVideosQueryHandler : IRequestHandler<GetVideosQuery, PagedResult<VideoDto>>
{
    private readonly IVideoService _videoService;

    public GetVideosQueryHandler(IVideoService videoService)
    {
        _videoService = videoService;
    }

    public async Task<PagedResult<VideoDto>> Handle(GetVideosQuery request, CancellationToken cancellationToken)
    {
        return await _videoService.GetVideosAsync(
            request.UserId,
            request.BaseUrl,
            request.Search,
            request.CategoryId,
            request.SortBy,
            request.MediaType,
            request.Page,
            request.PageSize,
            cancellationToken);
    }
}
