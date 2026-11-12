using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyTube.Backend.Domain;

namespace MyTube.Backend.Application.Repositories;

public interface IVideoRepository : IRepository<Video>
{
    Task<(IReadOnlyList<Video> Videos, int TotalCount)> GetPagedVideosAsync(
        string? search,
        int? categoryId,
        string? sortBy,
        string? mediaType,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<Video?> GetWithCategoryAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Video>> GetByIdsWithCategoryAsync(IEnumerable<Guid> ids, CancellationToken ct = default);

    Task<IReadOnlyList<Video>> GetFavoriteVideosAsync(string userId, CancellationToken ct = default);

    Task<IReadOnlyList<Video>> GetContinueWatchingVideosAsync(string userId, int limit, CancellationToken ct = default);

    Task<WatchProgress?> GetUserProgressAsync(string userId, Guid videoId, CancellationToken ct = default);

    Task SaveUserProgressAsync(string userId, Guid videoId, double positionSeconds, double durationSeconds, CancellationToken ct = default);

    Task<bool> ToggleFavoriteAsync(string userId, Guid videoId, CancellationToken ct = default);

    Task<Dictionary<Guid, WatchProgress>> GetProgressMapAsync(string userId, IEnumerable<Guid> videoIds, CancellationToken ct = default);

    Task<HashSet<Guid>> GetFavoriteIdsAsync(string userId, IEnumerable<Guid> videoIds, CancellationToken ct = default);

    Task<IReadOnlyList<VideoQa>> GetQaHistoryAsync(Guid videoId, CancellationToken ct = default);

    Task AddQaAsync(VideoQa qa, CancellationToken ct = default);
}
