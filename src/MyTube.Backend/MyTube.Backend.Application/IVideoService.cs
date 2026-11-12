using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyTube.Backend.Application;

public interface IVideoService
{
    Task<PagedResult<VideoDto>> GetVideosAsync(
        string userId,
        string baseUrl,
        string? search,
        int? categoryId,
        string? sortBy,
        string? mediaType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<VideoDto?> GetVideoByIdAsync(Guid id, string userId, string baseUrl, CancellationToken cancellationToken = default);
    
    Task<List<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    
    Task<List<VideoDto>> GetFavoritesAsync(string userId, string baseUrl, CancellationToken cancellationToken = default);
    
    Task<bool> ToggleFavoriteAsync(string userId, Guid videoId, CancellationToken cancellationToken = default);
    
    Task UpdateProgressAsync(string userId, Guid videoId, double positionSeconds, double durationSeconds, CancellationToken cancellationToken = default);
    
    Task<WatchProgressDto?> GetProgressAsync(string userId, Guid videoId, CancellationToken cancellationToken = default);
    
    Task<List<VideoDto>> GetContinueWatchingAsync(string userId, string baseUrl, int limit = 6, CancellationToken cancellationToken = default);
    
    Task<CategoryDto> CreateCategoryAsync(string name, CancellationToken cancellationToken = default);
    
    Task<bool> UpdateVideoCategoryAsync(Guid videoId, int categoryId, CancellationToken cancellationToken = default);

    Task<int> UpdateVideosCategoryAsync(IEnumerable<Guid> videoIds, int categoryId, CancellationToken cancellationToken = default);

    Task<int> DeleteVideosAsync(IEnumerable<Guid> videoIds, CancellationToken cancellationToken = default);
    
    Task<bool> RenameVideoAsync(Guid videoId, string newTitle, CancellationToken cancellationToken = default);

    Task<bool> DeleteCategoryAsync(int categoryId, CancellationToken cancellationToken = default);
}
