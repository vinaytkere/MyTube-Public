using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyTube.Backend.Application.Iptv;

namespace MyTube.Backend.Application.Features.Iptv.Queries;

public record GetIptvPlaylistM3uQuery(
    string? UserId,
    bool FavoritesOnly,
    string? CountryCode,
    string? CategoryId,
    string? LanguageCode,
    bool HttpsOnly) : IRequest<string>;

public class GetIptvPlaylistM3uQueryHandler : IRequestHandler<GetIptvPlaylistM3uQuery, string>
{
    private readonly IIptvService _iptvService;

    public GetIptvPlaylistM3uQueryHandler(IIptvService iptvService)
    {
        _iptvService = iptvService;
    }

    public async Task<string> Handle(GetIptvPlaylistM3uQuery request, CancellationToken cancellationToken)
    {
        return await _iptvService.GenerateM3uPlaylistAsync(
            request.UserId,
            request.FavoritesOnly,
            request.CountryCode,
            request.CategoryId,
            request.LanguageCode,
            request.HttpsOnly,
            cancellationToken);
    }
}

