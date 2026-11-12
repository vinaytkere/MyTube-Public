using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyTube.Backend.Application.Iptv;

public interface IIptvService
{
    Task<PagedResult<IptvChannelDto>> GetChannelsAsync(
        string? userId,
        string? scope,
        string? regionCode,
        string? countryCode,
        string? languageCode,
        string? categoryId,
        string? search,
        bool favoritesOnly,
        bool historyOnly,
        int page,
        int pageSize,
        bool httpsOnly = false,
        CancellationToken ct = default);

    Task<IptvChannelDto?> GetChannelByIdAsync(string channelId, string? userId, CancellationToken ct = default);
    Task<IptvFilterOptionsDto> GetFilterOptionsAsync(CancellationToken ct = default);
    Task<bool> ToggleFavoriteAsync(string userId, string channelId, CancellationToken ct = default);
    Task<List<string>> GetUserFavoriteChannelIdsAsync(string userId, CancellationToken ct = default);
    Task RecordWatchHistoryAsync(string userId, string channelId, int? streamId, CancellationToken ct = default);
    Task<bool> ReportStreamHealthAsync(int streamId, bool isWorking, string? errorMessage, CancellationToken ct = default);
    Task<string> GenerateM3uPlaylistAsync(
        string? userId,
        bool favoritesOnly,
        string? countryCode,
        string? categoryId,
        string? languageCode,
        bool httpsOnly = false,
        CancellationToken ct = default);
}

