using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyTube.Backend.Application.Repositories;
using MyTube.Backend.Domain;

namespace MyTube.Backend.Infrastructure.Repositories;

public class VideoRepository : EfRepository<Video>, IVideoRepository
{
    public VideoRepository(VideoDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<(IReadOnlyList<Video> Videos, int TotalCount)> GetPagedVideosAsync(
        string? search,
        int? categoryId,
        string? sortBy,
        string? mediaType,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = DbSet
            .Include(v => v.Category)
            .Where(v => v.ScanStatus == ScanState.Ready ||
                        v.ScanStatus == ScanState.Processing ||
                        v.ScanStatus == ScanState.Discovered ||
                        v.ScanStatus == ScanState.Failed);

        if (categoryId.HasValue)
        {
            query = query.Where(v => v.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(v => v.DisplayTitle.ToLower().Contains(searchLower) ||
                                     v.FileName.ToLower().Contains(searchLower));
        }

        if (!string.IsNullOrWhiteSpace(mediaType))
        {
            if (mediaType.Equals("audio", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(v => v.FileName.EndsWith(".mp3") || v.FileName.EndsWith(".m4a"));
            }
            else if (mediaType.Equals("video", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(v => !v.FileName.EndsWith(".mp3") && !v.FileName.EndsWith(".m4a"));
            }
        }

        query = sortBy?.ToLower() switch
        {
            "recentlyadded" => query.OrderByDescending(v => v.CreatedAt),
            "oldest" => query.OrderBy(v => v.CreatedAt),
            "title" => query.OrderBy(v => v.DisplayTitle),
            "duration" => query.OrderByDescending(v => v.Duration),
            _ => query.OrderByDescending(v => v.CreatedAt)
        };

        var totalCount = await query.CountAsync(ct);

        var videos = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (videos, totalCount);
    }

    public async Task<Video?> GetWithCategoryAsync(Guid id, CancellationToken ct = default)
    {
        return await DbSet.Include(v => v.Category).FirstOrDefaultAsync(v => v.Id == id, ct);
    }

    public async Task<IReadOnlyList<Video>> GetByIdsWithCategoryAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        return await DbSet.Include(v => v.Category).Where(v => ids.Contains(v.Id)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Video>> GetFavoriteVideosAsync(string userId, CancellationToken ct = default)
    {
        var favorites = await DbContext.Favorites
            .Include(f => f.Video)
            .ThenInclude(v => v!.Category)
            .Where(f => f.UserId == userId && f.Video != null && f.Video.ScanStatus != ScanState.Missing)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(ct);

        return favorites.Where(f => f.Video != null).Select(f => f.Video!).ToList();
    }

    public async Task<IReadOnlyList<Video>> GetContinueWatchingVideosAsync(string userId, int limit, CancellationToken ct = default)
    {
        var progressList = await DbContext.WatchProgresses
            .Include(wp => wp.Video)
            .ThenInclude(v => v!.Category)
            .Where(wp => wp.UserId == userId && !wp.Completed && wp.Video != null && wp.Video.ScanStatus != ScanState.Missing)
            .OrderByDescending(wp => wp.UpdatedAt)
            .Take(limit)
            .ToListAsync(ct);

        return progressList.Where(wp => wp.Video != null).Select(wp => wp.Video!).ToList();
    }

    public async Task<WatchProgress?> GetUserProgressAsync(string userId, Guid videoId, CancellationToken ct = default)
    {
        return await DbContext.WatchProgresses
            .FirstOrDefaultAsync(wp => wp.UserId == userId && wp.VideoId == videoId, ct);
    }

    public async Task SaveUserProgressAsync(string userId, Guid videoId, double positionSeconds, double durationSeconds, CancellationToken ct = default)
    {
        var progress = await DbContext.WatchProgresses
            .FirstOrDefaultAsync(wp => wp.UserId == userId && wp.VideoId == videoId, ct);

        bool completed = durationSeconds > 0 && (positionSeconds / durationSeconds >= 0.95);

        if (progress == null)
        {
            progress = new WatchProgress
            {
                UserId = userId,
                VideoId = videoId,
                PositionSeconds = positionSeconds,
                DurationSeconds = durationSeconds,
                Completed = completed,
                UpdatedAt = DateTime.UtcNow
            };
            await DbContext.WatchProgresses.AddAsync(progress, ct);
        }
        else
        {
            progress.PositionSeconds = positionSeconds;
            progress.DurationSeconds = durationSeconds;
            progress.Completed = completed;
            progress.UpdatedAt = DateTime.UtcNow;
        }

        await DbContext.SaveChangesAsync(ct);
    }

    public async Task<bool> ToggleFavoriteAsync(string userId, Guid videoId, CancellationToken ct = default)
    {
        var existing = await DbContext.Favorites
            .FirstOrDefaultAsync(f => f.UserId == userId && f.VideoId == videoId, ct);

        if (existing != null)
        {
            DbContext.Favorites.Remove(existing);
            await DbContext.SaveChangesAsync(ct);
            return false;
        }

        var favorite = new Favorite
        {
            UserId = userId,
            VideoId = videoId,
            CreatedAt = DateTime.UtcNow
        };
        await DbContext.Favorites.AddAsync(favorite, ct);
        await DbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<Dictionary<Guid, WatchProgress>> GetProgressMapAsync(string userId, IEnumerable<Guid> videoIds, CancellationToken ct = default)
    {
        var list = await DbContext.WatchProgresses
            .Where(wp => wp.UserId == userId && videoIds.Contains(wp.VideoId))
            .ToListAsync(ct);

        return list
            .GroupBy(wp => wp.VideoId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(wp => wp.UpdatedAt).First());
    }

    public async Task<HashSet<Guid>> GetFavoriteIdsAsync(string userId, IEnumerable<Guid> videoIds, CancellationToken ct = default)
    {
        var ids = await DbContext.Favorites
            .Where(f => f.UserId == userId && videoIds.Contains(f.VideoId))
            .Select(f => f.VideoId)
            .ToListAsync(ct);

        return ids.ToHashSet();
    }

    public async Task<IReadOnlyList<VideoQa>> GetQaHistoryAsync(Guid videoId, CancellationToken ct = default)
    {
        return await DbContext.VideoQas
            .Where(q => q.VideoId == videoId)
            .OrderBy(q => q.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddQaAsync(VideoQa qa, CancellationToken ct = default)
    {
        await DbContext.VideoQas.AddAsync(qa, ct);
        await DbContext.SaveChangesAsync(ct);
    }
}
