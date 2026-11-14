using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application;
using MyTube.Backend.Domain;
using MyTube.Backend.Infrastructure.Media;

namespace MyTube.Backend.Infrastructure;

public class VideoService : IVideoService
{
    private readonly VideoDbContext _dbContext;
    private readonly string _mediaRoot;
    private readonly IMediaProcessor? _mediaProcessor;
    private readonly ILogger<VideoService>? _logger;

    public VideoService(
        VideoDbContext dbContext, 
        IConfiguration configuration,
        IMediaProcessor? mediaProcessor = null,
        ILogger<VideoService>? logger = null)
    {
        _dbContext = dbContext;
        _mediaRoot = configuration["Media:RootPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "media");
        _mediaProcessor = mediaProcessor;
        _logger = logger;
    }

    public async Task<PagedResult<VideoDto>> GetVideosAsync(
        string userId,
        string baseUrl,
        string? search,
        int? categoryId,
        string? sortBy,
        string? mediaType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Videos
            .AsNoTracking()
            .Include(v => v.Category)
            .Where(v => v.ScanStatus == ScanState.Ready ||
                        v.ScanStatus == ScanState.Processing ||
                        v.ScanStatus == ScanState.Discovered ||
                        v.ScanStatus == ScanState.Failed);

        // Filter by category
        if (categoryId.HasValue)
        {
            query = query.Where(v => v.CategoryId == categoryId.Value);
        }

        // Filter by search using database-optimized index-compatible search
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchPattern = $"%{search.Trim()}%";
            query = query.Where(v => EF.Functions.Like(v.DisplayTitle, searchPattern) || 
                                     EF.Functions.Like(v.FileName, searchPattern));
        }

        // Filter by media type
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

        // Sorting
        query = sortBy?.ToLower() switch
        {
            "recentlyadded" => query.OrderByDescending(v => v.CreatedAt),
            "oldest" => query.OrderBy(v => v.CreatedAt),
            "title" => query.OrderBy(v => v.DisplayTitle),
            "duration" => query.OrderByDescending(v => v.Duration),
            _ => query.OrderByDescending(v => v.CreatedAt) // Default sorting
        };

        var totalCount = await query.CountAsync(cancellationToken);
        
        var videos = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Get user progress and favorites for the fetched page without tracking overhead
        var videoIds = videos.Select(v => v.Id).ToList();
        
        var progressMap = (await _dbContext.WatchProgresses
            .AsNoTracking()
            .Where(wp => wp.UserId == userId && videoIds.Contains(wp.VideoId))
            .ToListAsync(cancellationToken))
            .GroupBy(wp => wp.VideoId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(wp => wp.UpdatedAt).First());

        var favoritesSet = (await _dbContext.Favorites
            .AsNoTracking()
            .Where(f => f.UserId == userId && videoIds.Contains(f.VideoId))
            .Select(f => f.VideoId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var dtos = videos.Select(v => MapToDto(v, userId, baseUrl, progressMap.GetValueOrDefault(v.Id), favoritesSet.Contains(v.Id))).ToList();

        return new PagedResult<VideoDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<VideoDto?> GetVideoByIdAsync(Guid id, string userId, string baseUrl, CancellationToken cancellationToken = default)
    {
        var video = await _dbContext.Videos
            .AsNoTracking()
            .Include(v => v.Category)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

        if (video == null) return null;

        if (_mediaProcessor != null && (string.IsNullOrWhiteSpace(video.AudioTracksJson) || video.AudioTracksJson == "[]"))
        {
            var physicalPath = _mediaProcessor.ResolvePhysicalFilePath(video.FilePath);
            if (System.IO.File.Exists(physicalPath))
            {
                try
                {
                    var meta = await _mediaProcessor.ExtractMetadataAsync(physicalPath);
                    if (meta != null && meta.AudioTracks != null && meta.AudioTracks.Count > 0)
                    {
                        video.AudioTracksJson = System.Text.Json.JsonSerializer.Serialize(meta.AudioTracks);
                        if (!string.IsNullOrWhiteSpace(meta.AudioCodec))
                        {
                            video.AudioCodec = meta.AudioCodec;
                        }
                        if (!string.IsNullOrWhiteSpace(meta.VideoCodec))
                        {
                            video.VideoCodec = meta.VideoCodec;
                        }

                        var dbVid = await _dbContext.Videos.FirstOrDefaultAsync(v => v.Id == video.Id, cancellationToken);
                        if (dbVid != null)
                        {
                            dbVid.AudioTracksJson = video.AudioTracksJson;
                            dbVid.AudioCodec = video.AudioCodec;
                            dbVid.VideoCodec = video.VideoCodec;
                            await _dbContext.SaveChangesAsync(cancellationToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed on-the-fly audio probe for video {VideoId}", video.Id);
                }
            }
        }

        var progress = await _dbContext.WatchProgresses
            .AsNoTracking()
            .FirstOrDefaultAsync(wp => wp.UserId == userId && wp.VideoId == id, cancellationToken);

        var isFavorite = await _dbContext.Favorites
            .AsNoTracking()
            .AnyAsync(f => f.UserId == userId && f.VideoId == id, cancellationToken);

        return MapToDto(video, userId, baseUrl, progress, isFavorite);
    }

    public async Task<List<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Categories
            .AsNoTracking()
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                VideoCount = c.Videos.Count(v => v.ScanStatus != ScanState.Missing)
            })
            .Where(c => c.VideoCount > 0)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<CategoryDto> CreateCategoryAsync(string name, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower(), cancellationToken);
        if (existing != null)
        {
            return new CategoryDto
            {
                Id = existing.Id,
                Name = existing.Name,
                VideoCount = existing.Videos.Count(v => v.ScanStatus != ScanState.Missing)
            };
        }

        var category = new Category
        {
            Name = name,
            CreatedAt = DateTime.UtcNow,
            IsUserCreated = true
        };
        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            VideoCount = 0
        };
    }

    public async Task<bool> UpdateVideoCategoryAsync(Guid videoId, int categoryId, CancellationToken cancellationToken = default)
    {
        var video = await _dbContext.Videos.FindAsync(new object[] { videoId }, cancellationToken);
        if (video == null) return false;

        var category = await _dbContext.Categories.FindAsync(new object[] { categoryId }, cancellationToken);
        if (category == null) return false;

        video.CategoryId = categoryId;
        video.UpdatedAt = DateTime.UtcNow;
        
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> UpdateVideosCategoryAsync(IEnumerable<Guid> videoIds, int categoryId, CancellationToken cancellationToken = default)
    {
        var categoryExists = await _dbContext.Categories.AnyAsync(c => c.Id == categoryId, cancellationToken);
        if (!categoryExists) return 0;

        var idList = videoIds as IList<Guid> ?? videoIds.ToList();
        if (idList.Count == 0) return 0;

        if (_dbContext.Database.ProviderName != null && _dbContext.Database.ProviderName.Contains("InMemory"))
        {
            var videos = await _dbContext.Videos.Where(v => idList.Contains(v.Id)).ToListAsync(cancellationToken);
            foreach (var v in videos)
            {
                v.CategoryId = categoryId;
                v.UpdatedAt = DateTime.UtcNow;
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
            return videos.Count;
        }

        return await _dbContext.Videos
            .Where(v => idList.Contains(v.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(v => v.CategoryId, categoryId)
                .SetProperty(v => v.UpdatedAt, DateTime.UtcNow), cancellationToken);
    }

    public async Task<List<VideoDto>> GetFavoritesAsync(string userId, string baseUrl, CancellationToken cancellationToken = default)
    {
        var favorites = await _dbContext.Favorites
            .AsNoTracking()
            .Include(f => f.Video)
            .ThenInclude(v => v!.Category)
            .Where(f => f.UserId == userId && f.Video != null && f.Video.ScanStatus != ScanState.Missing)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(cancellationToken);

        var videoIds = favorites.Select(f => f.VideoId).ToList();

        var progressMap = (await _dbContext.WatchProgresses
            .AsNoTracking()
            .Where(wp => wp.UserId == userId && videoIds.Contains(wp.VideoId))
            .ToListAsync(cancellationToken))
            .GroupBy(wp => wp.VideoId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(wp => wp.UpdatedAt).First());

        return favorites
            .Where(f => f.Video != null)
            .Select(f => MapToDto(f.Video!, userId, baseUrl, progressMap.GetValueOrDefault(f.VideoId), true))
            .ToList();
    }

    public async Task<bool> ToggleFavoriteAsync(string userId, Guid videoId, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.Favorites
            .FirstOrDefaultAsync(f => f.UserId == userId && f.VideoId == videoId, cancellationToken);

        if (existing != null)
        {
            _dbContext.Favorites.Remove(existing);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return false; // Not favorite anymore
        }

        var favorite = new Favorite
        {
            UserId = userId,
            VideoId = videoId
        };
        _dbContext.Favorites.Add(favorite);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true; // Now favorite
    }

    public async Task UpdateProgressAsync(string userId, Guid videoId, double positionSeconds, double durationSeconds, CancellationToken cancellationToken = default)
    {
        var progress = await _dbContext.WatchProgresses
            .FirstOrDefaultAsync(wp => wp.UserId == userId && wp.VideoId == videoId, cancellationToken);

        // Consider video "completed" if user watched 95% of it
        bool completed = positionSeconds / durationSeconds >= 0.95;

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
            _dbContext.WatchProgresses.Add(progress);
        }
        else
        {
            progress.PositionSeconds = positionSeconds;
            progress.DurationSeconds = durationSeconds;
            progress.Completed = completed;
            progress.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<WatchProgressDto?> GetProgressAsync(string userId, Guid videoId, CancellationToken cancellationToken = default)
    {
        var progress = await _dbContext.WatchProgresses
            .AsNoTracking()
            .FirstOrDefaultAsync(wp => wp.UserId == userId && wp.VideoId == videoId, cancellationToken);

        if (progress == null) return null;

        return new WatchProgressDto
        {
            VideoId = progress.VideoId,
            PositionSeconds = progress.PositionSeconds,
            DurationSeconds = progress.DurationSeconds,
            Completed = progress.Completed,
            UpdatedAt = progress.UpdatedAt
        };
    }

    public async Task<List<VideoDto>> GetContinueWatchingAsync(string userId, string baseUrl, int limit = 6, CancellationToken cancellationToken = default)
    {
        var progressList = await _dbContext.WatchProgresses
            .AsNoTracking()
            .Include(wp => wp.Video)
            .ThenInclude(v => v!.Category)
            .Where(wp => wp.UserId == userId && !wp.Completed && wp.Video != null && wp.Video.ScanStatus != ScanState.Missing)
            .OrderByDescending(wp => wp.UpdatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var videoIds = progressList.Select(wp => wp.VideoId).ToList();

        var favoritesSet = (await _dbContext.Favorites
            .AsNoTracking()
            .Where(f => f.UserId == userId && videoIds.Contains(f.VideoId))
            .Select(f => f.VideoId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        return progressList
            .Where(wp => wp.Video != null)
            .Select(wp => MapToDto(wp.Video!, userId, baseUrl, wp, favoritesSet.Contains(wp.VideoId)))
            .ToList();
    }

    private VideoDto MapToDto(Video video, string userId, string baseUrl, WatchProgress? progress, bool isFavorite)
    {
        var audioTracks = new List<AudioTrackDto>();
        if (!string.IsNullOrEmpty(video.AudioTracksJson))
        {
            try
            {
                var parsed = System.Text.Json.JsonSerializer.Deserialize<List<AudioTrackDto>>(video.AudioTracksJson);
                if (parsed != null)
                {
                    audioTracks = parsed;
                }
            }
            catch { }
        }

        string qualityLabel;
        if (video.FileName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) || video.FileName.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase) || video.Height == 0)
        {
            qualityLabel = "Audio MP3";
        }
        else
        {
            qualityLabel = video.Height switch
            {
                >= 2160 => "4K",
                >= 1440 => "2K",
                >= 1080 => "1080p",
                >= 720 => "720p",
                >= 480 => "480p",
                >= 360 => "360p",
                > 0 => $"{video.Height}p",
                _ => "SD"
            };
        }

        var tagsList = new List<string>();
        if (!string.IsNullOrEmpty(video.TagsJson))
        {
            try
            {
                var parsedTags = System.Text.Json.JsonSerializer.Deserialize<List<string>>(video.TagsJson);
                if (parsedTags != null) tagsList = parsedTags;
            }
            catch { }
        }

        return new VideoDto
        {
            Id = video.Id,
            FileName = video.FileName,
            DisplayTitle = video.DisplayTitle,
            FileSize = video.FileSize,
            Duration = video.Duration,
            Width = video.Width,
            Height = video.Height,
            VideoCodec = video.VideoCodec,
            AudioCodec = video.AudioCodec,
            ContainerFormat = video.ContainerFormat,
            FrameRate = video.FrameRate,
            CategoryId = video.CategoryId,
            CategoryName = video.Category?.Name ?? "Other",
            ScanStatus = video.ScanStatus.ToString(),
            SourceUrl = video.SourceUrl,
            Quality = qualityLabel,
            StreamUrl = $"{baseUrl}/api/videos/{video.Id}/stream",
            DownloadUrl = $"{baseUrl}/api/videos/{video.Id}/download",
            ThumbnailUrl = $"{baseUrl}/api/videos/{video.Id}/thumbnail",
            ProgressSeconds = progress?.PositionSeconds,
            Completed = progress?.Completed,
            IsFavorite = isFavorite,
            Transcript = video.Transcript,
            Summary = video.Summary,
            Tags = tagsList,
            DetectedLanguage = video.DetectedLanguage,
            AiStatus = video.AiStatus,
            ProcessingError = video.ProcessingError,
            AiProcessedAt = video.AiProcessedAt,
            CreatedAt = video.CreatedAt,
            MediaTypeDetected = video.MediaTypeDetected,
            PrimaryGenre = video.PrimaryGenre,
            ReleaseYear = video.ReleaseYear,
            IsOrganized = video.IsOrganized,
            OrganizedPath = video.OrganizedPath,
            Uploader = video.Uploader,
            AudioTracks = audioTracks,
            SubtitleTracks = DiscoverSubtitleTracks(video, baseUrl)
        };
    }

    private List<SubtitleTrackDto> DiscoverSubtitleTracks(Video video, string baseUrl)
    {
        var tracks = new List<SubtitleTrackDto>();
        try
        {
            var resolvedPath = _mediaProcessor?.ResolvePhysicalFilePath(video.FilePath) ?? ResolvePhysicalPath(video.FilePath);
            if (string.IsNullOrEmpty(resolvedPath) || !File.Exists(resolvedPath))
            {
                return tracks;
            }

            var dir = Path.GetDirectoryName(resolvedPath);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return tracks;

            var baseName = Path.GetFileNameWithoutExtension(resolvedPath);

            var subFiles = Directory.GetFiles(dir, "*.*")
                .Where(f => (f.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".srt", StringComparison.OrdinalIgnoreCase))
                            && Path.GetFileName(f).StartsWith(baseName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f.Contains("-orig", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ThenBy(f => f.Length)
                .ThenBy(f => f)
                .ToList();

            int trackIdx = 0;
            foreach (var subFile in subFiles)
            {
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(subFile);
                var langCode = "en";
                var label = "English";

                if (fileNameWithoutExt.Length > baseName.Length && fileNameWithoutExt[baseName.Length] == '.')
                {
                    var langPart = fileNameWithoutExt.Substring(baseName.Length + 1).Trim();
                    if (!string.IsNullOrWhiteSpace(langPart))
                    {
                        langCode = langPart;
                        label = GetLanguageDisplayName(langPart);
                    }
                }

                tracks.Add(new SubtitleTrackDto
                {
                    Id = $"{video.Id}-sub-{trackIdx}",
                    Src = $"{baseUrl}/api/videos/{video.Id}/subtitles/{trackIdx}",
                    Label = label,
                    Language = langCode,
                    IsDefault = (trackIdx == 0)
                });

                trackIdx++;
            }
        }
        catch { }

        return tracks;
    }

    private string ResolvePhysicalPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return string.Empty;

        var normalizedPath = filePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        if (File.Exists(normalizedPath)) return normalizedPath;
        if (File.Exists(filePath)) return filePath;

        try
        {
            var fileName = Path.GetFileName(normalizedPath);
            if (string.IsNullOrWhiteSpace(fileName)) return filePath;

            var searchDirs = new[]
            {
                _mediaRoot,
                "/media",
                "/app/media",
                Path.Combine(_mediaRoot, "Downloads"),
                "/media/Downloads",
                "/app/media/Downloads",
                Path.Combine(Directory.GetCurrentDirectory(), "media"),
                Path.Combine(Directory.GetCurrentDirectory(), "media", "Downloads"),
                Directory.GetCurrentDirectory()
            }.Where(d => !string.IsNullOrWhiteSpace(d) && Directory.Exists(d)).Distinct();

            foreach (var dir in searchDirs)
            {
                var directCandidate = Path.Combine(dir, fileName);
                if (File.Exists(directCandidate)) return directCandidate;

                var candidates = Directory.GetFiles(dir, fileName, SearchOption.AllDirectories);
                if (candidates.Length > 0) return candidates[0];

                var targetStem = Path.GetFileNameWithoutExtension(fileName);
                var targetNormalized = System.Text.RegularExpressions.Regex.Replace(targetStem, @"\s+", " ").Trim().ToLower();

                var allFiles = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
                foreach (var cand in allFiles)
                {
                    var candStem = Path.GetFileNameWithoutExtension(cand);
                    var candNormalized = System.Text.RegularExpressions.Regex.Replace(candStem, @"\s+", " ").Trim().ToLower();

                    if (candNormalized == targetNormalized || candNormalized.Contains(targetNormalized) || targetNormalized.Contains(candNormalized))
                    {
                        return cand;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "ResolvePhysicalPath error for {FilePath}", filePath);
        }

        return filePath;
    }

    private static string GetLanguageDisplayName(string langCode)
    {
        var normalized = langCode.ToLowerInvariant();
        var isAutoOrig = normalized.EndsWith("-orig");
        var cleanCode = isAutoOrig ? normalized[..^5] : normalized;

        string name = cleanCode switch
        {
            _ when cleanCode.StartsWith("en") => "English",
            _ when cleanCode.StartsWith("es") => "Spanish",
            _ when cleanCode.StartsWith("fr") => "French",
            _ when cleanCode.StartsWith("de") => "German",
            _ when cleanCode.StartsWith("it") => "Italian",
            _ when cleanCode.StartsWith("pt") => "Portuguese",
            _ when cleanCode.StartsWith("ru") => "Russian",
            _ when cleanCode.StartsWith("ja") => "Japanese",
            _ when cleanCode.StartsWith("ko") => "Korean",
            _ when cleanCode.StartsWith("zh") => "Chinese",
            _ when cleanCode.StartsWith("hi") => "Hindi",
            _ when cleanCode.StartsWith("kn") => "Kannada",
            _ when cleanCode.StartsWith("ar") => "Arabic",
            _ => cleanCode.ToUpperInvariant()
        };

        return isAutoOrig ? $"{name} (Auto-generated)" : name;
    }

    public async Task<int> DeleteVideosAsync(IEnumerable<Guid> videoIds, CancellationToken cancellationToken = default)
    {
        var videos = await _dbContext.Videos
            .Where(v => videoIds.Contains(v.Id))
            .ToListAsync(cancellationToken);

        int deletedCount = 0;
        var affectedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Force GC cleanup to release any pending file stream or image locks
        GC.Collect();
        GC.WaitForPendingFinalizers();

        foreach (var video in videos)
        {
            try
            {
                var dir = Path.GetDirectoryName(video.FilePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    affectedDirectories.Add(dir);
                }

                if (System.IO.File.Exists(video.FilePath))
                {
                    try
                    {
                        System.IO.File.SetAttributes(video.FilePath, FileAttributes.Normal);
                    }
                    catch { }
                    System.IO.File.Delete(video.FilePath);
                }

                if (!string.IsNullOrEmpty(video.ThumbnailPath) && System.IO.File.Exists(video.ThumbnailPath))
                {
                    try
                    {
                        System.IO.File.SetAttributes(video.ThumbnailPath, FileAttributes.Normal);
                    }
                    catch { }
                    System.IO.File.Delete(video.ThumbnailPath);
                }

                _dbContext.Videos.Remove(video);
                deletedCount++;
            }
            catch
            {
                // Continue deleting other files even if one throws
            }
        }

        if (deletedCount > 0)
        {
            await _dbContext.SaveChangesAsync();
        }

        // Clean up any directories that became empty after video removal
        foreach (var dir in affectedDirectories)
        {
            TryCleanEmptyDirectory(dir);
        }

        return deletedCount;
    }

    public async Task<bool> RenameVideoAsync(Guid videoId, string newTitle, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newTitle)) return false;

        var video = await _dbContext.Videos.FindAsync(new object[] { videoId }, cancellationToken);
        if (video == null) return false;

        video.DisplayTitle = newTitle.Trim();

        var oldFilePath = video.FilePath;
        if (System.IO.File.Exists(oldFilePath))
        {
            try
            {
                var directory = Path.GetDirectoryName(oldFilePath);
                var extension = Path.GetExtension(oldFilePath);
                
                var safeFileName = SafePathHelper.SanitizeFileName(newTitle, SafePathHelper.DefaultMaxStemBytes, "Video");

                var newFileName = safeFileName + extension;
                var newFilePath = Path.Combine(directory ?? string.Empty, newFileName);

                if (oldFilePath != newFilePath)
                {
                    if (System.IO.File.Exists(newFilePath))
                    {
                        System.IO.File.Delete(newFilePath);
                    }
                    System.IO.File.Move(oldFilePath, newFilePath);
                    video.FilePath = newFilePath;
                    video.FileName = newFileName;
                }
            }
            catch
            {
                // Proceed with title DB update if file system renaming fails
            }
        }

        video.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteCategoryAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.Categories
            .Include(c => c.Videos)
            .FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken);

        if (category == null) return false;

        // Strictly protect system non-deletable folders (Radarr, Downloads)
        if (category.Name.Equals("Radarr", StringComparison.OrdinalIgnoreCase) ||
            category.Name.Equals("Downloads", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"The '{category.Name}' playlist is a protected non-deletable system folder.");
        }

        // Ensure default Downloads category exists
        var defaultCategory = await _dbContext.Categories
            .FirstOrDefaultAsync(c => c.Name == "Downloads", cancellationToken);

        if (defaultCategory == null)
        {
            defaultCategory = new Category { Name = "Downloads", IsUserCreated = false };
            _dbContext.Categories.Add(defaultCategory);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // Safely reassign all videos in this category to default Downloads category so media files on disk are completely safe
        foreach (var video in category.Videos)
        {
            video.CategoryId = defaultCategory.Id;
            video.UpdatedAt = DateTime.UtcNow;
        }

        // Remove the playlist/category record without deleting media files from disk
        _dbContext.Categories.Remove(category);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private void TryCleanEmptyDirectory(string dirPath)
    {
        try
        {
            if (!Directory.Exists(dirPath)) return;

            var normalizedDir = Path.GetFullPath(dirPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedMediaRoot = Path.GetFullPath(_mediaRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedDownloads = Path.GetFullPath(Path.Combine(_mediaRoot, "Downloads")).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (normalizedDir == normalizedMediaRoot || normalizedDir == normalizedDownloads) return;

            var supportedExtensions = new[] { ".mp4", ".mkv", ".avi", ".mov", ".webm", ".flv", ".m4v", ".3gp", ".mp3", ".m4a", ".opus", ".wav", ".ogg", ".flac", ".aac" };
            var hasMediaFiles = Directory.EnumerateFiles(normalizedDir, "*.*", SearchOption.AllDirectories)
                .Any(f => supportedExtensions.Contains(Path.GetExtension(f).ToLower()));

            if (!hasMediaFiles)
            {
                foreach (var file in Directory.GetFiles(normalizedDir, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        System.IO.File.SetAttributes(file, FileAttributes.Normal);
                        System.IO.File.Delete(file);
                    }
                    catch { }
                }
                Directory.Delete(normalizedDir, true);
            }
        }
        catch { }
    }
}
