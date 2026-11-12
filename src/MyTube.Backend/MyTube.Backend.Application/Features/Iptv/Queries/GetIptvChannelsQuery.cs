using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Iptv;

namespace MyTube.Backend.Application.Features.Iptv.Queries;

public record GetIptvChannelsQuery(
    string? UserId,
    string? Scope,
    string? RegionCode,
    string? CountryCode,
    string? LanguageCode,
    string? CategoryId,
    string? Search,
    bool FavoritesOnly,
    bool HistoryOnly,
    int Page = 1,
    int PageSize = 30,
    bool HttpsOnly = false) : IRequest<PagedResult<IptvChannelDto>>;

public class GetIptvChannelsQueryValidator : AbstractValidator<GetIptvChannelsQuery>
{
    public GetIptvChannelsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");
    }
}

public class GetIptvChannelsQueryHandler : IRequestHandler<GetIptvChannelsQuery, PagedResult<IptvChannelDto>>
{
    private readonly IIptvService _iptvService;

    public GetIptvChannelsQueryHandler(IIptvService iptvService)
    {
        _iptvService = iptvService;
    }

    public async Task<PagedResult<IptvChannelDto>> Handle(GetIptvChannelsQuery request, CancellationToken cancellationToken)
    {
        return await _iptvService.GetChannelsAsync(
            request.UserId,
            request.Scope,
            request.RegionCode,
            request.CountryCode,
            request.LanguageCode,
            request.CategoryId,
            request.Search,
            request.FavoritesOnly,
            request.HistoryOnly,
            request.Page,
            request.PageSize,
            request.HttpsOnly,
            cancellationToken);
    }
}

