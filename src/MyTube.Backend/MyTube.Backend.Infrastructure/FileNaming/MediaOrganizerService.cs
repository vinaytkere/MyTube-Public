using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MyTube.Backend.Application.AI;
using MyTube.Backend.Application.MediaOrganizer;
using MyTube.Backend.Application.MediaOrganizer.Dtos;
using MyTube.Backend.Domain;

namespace MyTube.Backend.Infrastructure.FileNaming;

public class MediaOrganizerService : IMediaOrganizerService
{
    private readonly VideoDbContext _dbContext;
    private readonly ISmartRouteClient _smartRouteClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MediaOrganizerService> _logger;
    private readonly MediaMetadataHarvester _metadataHarvester;

    private MediaOrganizerSettingsDto _settings;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public static readonly HashSet<string> ValidVideoCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Movies", "TV Shows", "Web Series", "Documentaries", "Short Films", "Comedy", "Educational", "Music Videos", "Personal Videos", "Unclassified"
    };

    public static readonly HashSet<string> ValidAudioCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Songs", "Podcasts", "Audiobooks", "Devotional", "Classical", "Comedy", "Soundtracks", "Recordings", "Unclassified"
    };

    public MediaOrganizerService(
        VideoDbContext dbContext,
        ISmartRouteClient smartRouteClient,
        IConfiguration configuration,
        ILogger<MediaOrganizerService> logger,
        MediaMetadataHarvester? metadataHarvester = null)
    {
        _dbContext = dbContext;
        _smartRouteClient = smartRouteClient;
        _configuration = configuration;
        _logger = logger;
        _metadataHarvester = metadataHarvester ?? new MediaMetadataHarvester(new NullMediaToolRunner(), NullLogger<MediaMetadataHarvester>.Instance);

        _settings = new MediaOrganizerSettingsDto
        {
            AutoOrganizeOnDownloadComplete = !bool.TryParse(configuration["Media:Organizer:AutoOrganizeOnDownloadComplete"], out var auto) || auto,
            MovieFolderPattern = configuration["Media:Organizer:MovieFolderPattern"] ?? "Videos/Movies",
            AudioFolderPattern = configuration["Media:Organizer:AudioFolderPattern"] ?? "Audio/Songs",
            MovieFilePattern = configuration["Media:Organizer:MovieFilePattern"] ?? "{Language}-{Title}-{Year}",
            AudioFilePattern = configuration["Media:Organizer:AudioFilePattern"] ?? "{Language}-{Title}-{Artist}",
            GenerateNfoFile = !bool.TryParse(configuration["Media:Organizer:GenerateNfoFile"], out var nfo) || nfo,
            MoveSubtitles = !bool.TryParse(configuration["Media:Organizer:MoveSubtitles"], out var subs) || subs
        };
    }

    private string MediaRoot => _configuration["Media:RootPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "media");

    public MediaOrganizerSettingsDto GetSettings() => _settings;

    public async Task UpdateSettingsAsync(MediaOrganizerSettingsDto settings, CancellationToken cancellationToken = default)
    {
        _settings = settings;
        try
        {
            var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(appSettingsPath))
            {
                var json = await File.ReadAllTextAsync(appSettingsPath, cancellationToken);
                var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json, _jsonOptions) ?? new();
                dict["Media:Organizer:AutoOrganizeOnDownloadComplete"] = _settings.AutoOrganizeOnDownloadComplete.ToString();
                dict["Media:Organizer:MovieFolderPattern"] = _settings.MovieFolderPattern;
                dict["Media:Organizer:AudioFolderPattern"] = _settings.AudioFolderPattern;
                dict["Media:Organizer:MovieFilePattern"] = _settings.MovieFilePattern;
                dict["Media:Organizer:AudioFilePattern"] = _settings.AudioFilePattern;
                dict["Media:Organizer:GenerateNfoFile"] = _settings.GenerateNfoFile.ToString();
                dict["Media:Organizer:MoveSubtitles"] = _settings.MoveSubtitles.ToString();

                await File.WriteAllTextAsync(appSettingsPath, JsonSerializer.Serialize(dict, _jsonOptions), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist media organizer settings to appsettings.json. Settings kept in-memory.");
        }
    }

    public async Task<MediaRenamePreviewDto> PreviewOrganizationAsync(Guid videoId, CancellationToken cancellationToken = default)
    {
        var video = await _dbContext.Videos.Include(v => v.Category).FirstOrDefaultAsync(v => v.Id == videoId, cancellationToken);
        if (video == null)
        {
            throw new KeyNotFoundException($"Video with ID '{videoId}' was not found.");
        }

        var classification = await GetOrClassifyMediaAsync(video, cancellationToken);
        var (proposedFileName, proposedRelativePath) = ComputeDestinationPaths(video, classification);

        var currentRelative = GetRelativePathFromMediaRoot(video.FilePath);
        var willChange = !string.Equals(currentRelative, proposedRelativePath, StringComparison.OrdinalIgnoreCase);

        var proposedFullDiskPath = Path.Combine(MediaRoot, proposedRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var hasConflict = willChange && File.Exists(proposedFullDiskPath) && !string.Equals(proposedFullDiskPath, video.FilePath, StringComparison.OrdinalIgnoreCase);

        return new MediaRenamePreviewDto
        {
            VideoId = video.Id,
            CurrentTitle = video.DisplayTitle,
            CurrentFileName = video.FileName,
            CurrentRelativePath = currentRelative,
            ProposedTitle = classification.CanonicalTitle,
            ProposedFileName = proposedFileName,
            ProposedRelativePath = proposedRelativePath,
            MediaType = classification.MediaType,
            Category = classification.Category,
            Language = classification.Language,
            PrimaryGenre = classification.PrimaryGenre,
            ReleaseYear = classification.ReleaseYear,
            Artist = classification.Artist ?? classification.ArtistOrDirector,
            Summary = classification.Summary,
            Confidence = classification.Confidence,
            ClassificationReason = classification.ClassificationReason,
            WillChange = willChange,
            HasConflict = hasConflict,
            ConflictReason = hasConflict ? "A file already exists at the target destination on disk." : null
        };
    }

    public async Task<List<MediaRenamePreviewDto>> BatchPreviewAsync(IEnumerable<Guid>? videoIds = null, CancellationToken cancellationToken = default)
    {
        IQueryable<Video> query = _dbContext.Videos.Include(v => v.Category);

        if (videoIds != null && videoIds.Any())
        {
            var idList = videoIds.ToList();
            query = query.Where(v => idList.Contains(v.Id));
        }

        var videos = await query.ToListAsync(cancellationToken);
        var previews = new List<MediaRenamePreviewDto>();

        foreach (var v in videos)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var p = await PreviewOrganizationAsync(v.Id, cancellationToken);
                previews.Add(p);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to preview organization for video {Id}", v.Id);
            }
        }

        return previews;
    }

    public async Task<MediaOrganizeResultDto> OrganizeMediaAsync(Guid videoId, CancellationToken cancellationToken = default)
    {
        var video = await _dbContext.Videos.Include(v => v.Category).FirstOrDefaultAsync(v => v.Id == videoId, cancellationToken);
        if (video == null)
        {
            return new MediaOrganizeResultDto
            {
                VideoId = videoId,
                Success = false,
                ErrorMessage = $"Video with ID '{videoId}' not found."
            };
        }

        if (!File.Exists(video.FilePath))
        {
            return new MediaOrganizeResultDto
            {
                VideoId = video.Id,
                OriginalFileName = video.FileName,
                OriginalPath = video.FilePath,
                Success = false,
                ErrorMessage = $"Source media file does not exist on disk: {video.FilePath}"
            };
        }

        try
        {
            // 1. Get or execute classification via OCI SmartRoute
            var classification = await GetOrClassifyMediaAsync(video, cancellationToken);

            // 2. Compute destination file and directory paths
            var (targetFileName, targetRelativePath) = ComputeDestinationPaths(video, classification);
            var targetFullPath = Path.Combine(MediaRoot, targetRelativePath.Replace('/', Path.DirectorySeparatorChar));

            // Avoid collisions if destination file already exists and is not current file
            targetFullPath = AvoidCollision(targetFullPath, video.FilePath);
            targetFileName = Path.GetFileName(targetFullPath);
            var targetDir = Path.GetDirectoryName(targetFullPath)!;

            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            var oldFilePath = video.FilePath;
            var oldFileName = video.FileName;
            var oldDir = Path.GetDirectoryName(oldFilePath);

            // 3. Move primary media file
            if (!string.Equals(oldFilePath, targetFullPath, StringComparison.OrdinalIgnoreCase))
            {
                File.Move(oldFilePath, targetFullPath, overwrite: true);
                _logger.LogInformation("Organized media file: '{Old}' -> '{New}'", oldFilePath, targetFullPath);
            }

            // 4. Move companion subtitles if requested
            if (_settings.MoveSubtitles && !string.IsNullOrEmpty(oldDir) && Directory.Exists(oldDir))
            {
                MoveCompanionSubtitles(oldDir, Path.GetFileNameWithoutExtension(oldFileName), targetDir, Path.GetFileNameWithoutExtension(targetFileName));
            }

            // 5. Generate Plex/Kodi .nfo metadata file
            if (_settings.GenerateNfoFile)
            {
                await GenerateNfoFileAsync(targetDir, targetFileName, classification, cancellationToken);
            }

            // 6. Update database record
            video.DisplayTitle = classification.CanonicalTitle;
            video.FileName = targetFileName;
            video.FilePath = targetFullPath;
            video.Summary = classification.Summary;
            video.PrimaryGenre = classification.PrimaryGenre;
            video.ReleaseYear = classification.ReleaseYear;
            video.DetectedLanguage = classification.Language;
            video.MediaTypeDetected = classification.MediaType;
            video.IsOrganized = true;
            video.OrganizedPath = GetRelativePathFromMediaRoot(targetFullPath);
            video.UpdatedAt = DateTime.UtcNow;

            // 7. Update Category in DB (strictly canonical category, e.g. "Movies", "Songs", "Classical", "Devotional")
            var targetCategoryName = classification.Category;
            var cat = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Name.ToLower() == targetCategoryName.ToLower(), cancellationToken);
            if (cat == null)
            {
                cat = new Category { Name = targetCategoryName, CreatedAt = DateTime.UtcNow, IsUserCreated = false };
                _dbContext.Categories.Add(cat);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            video.CategoryId = cat.Id;

            await _dbContext.SaveChangesAsync(cancellationToken);

            // 8. Clean up empty source directory if it was a custom subfolder
            CleanEmptyParentDirectories(oldDir);

            return new MediaOrganizeResultDto
            {
                VideoId = video.Id,
                OriginalFileName = oldFileName,
                NewFileName = targetFileName,
                OriginalPath = oldFilePath,
                NewPath = targetFullPath,
                MediaType = classification.MediaType,
                Category = classification.Category,
                Language = classification.Language,
                PrimaryGenre = classification.PrimaryGenre,
                ReleaseYear = classification.ReleaseYear,
                Artist = classification.Artist ?? classification.ArtistOrDirector,
                Summary = classification.Summary,
                Confidence = classification.Confidence,
                ClassificationReason = classification.ClassificationReason,
                ActionTaken = "RenamedAndMoved",
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed organizing media for video ID {VideoId}", video.Id);
            return new MediaOrganizeResultDto
            {
                VideoId = video.Id,
                OriginalFileName = video.FileName,
                OriginalPath = video.FilePath,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<List<MediaOrganizeResultDto>> BatchOrganizeAsync(IEnumerable<Guid>? videoIds = null, CancellationToken cancellationToken = default)
    {
        IQueryable<Video> query = _dbContext.Videos.Include(v => v.Category);

        if (videoIds != null && videoIds.Any())
        {
            var idList = videoIds.ToList();
            query = query.Where(v => idList.Contains(v.Id));
        }
        else
        {
            query = query.Where(v => !v.IsOrganized);
        }

        var videos = await query.ToListAsync(cancellationToken);
        var results = new List<MediaOrganizeResultDto>();

        foreach (var v in videos)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var res = await OrganizeMediaAsync(v.Id, cancellationToken);
            results.Add(res);
        }

        return results;
    }

    public async Task<CategoryConsolidationResultDto> ConsolidateCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var result = new CategoryConsolidationResultDto();
        try
        {
            _logger.LogInformation("Starting category consolidation and empty folder cleanup...");

            // 1. Fetch all categories and their videos
            var categories = await _dbContext.Categories
                .Include(c => c.Videos)
                .ToListAsync(cancellationToken);

            foreach (var cat in categories.ToList())
            {
                var catName = cat.Name.Trim();
                string? targetCanonical = null;

                if (catName.StartsWith("Audio - ", StringComparison.OrdinalIgnoreCase) || catName.StartsWith("Audio-", StringComparison.OrdinalIgnoreCase))
                {
                    var sub = catName.Substring(catName.IndexOf('-') + 1).Trim();
                    targetCanonical = NormalizeCategory(sub, isAudio: true);
                }
                else if (catName.StartsWith("Movies - ", StringComparison.OrdinalIgnoreCase) || catName.StartsWith("Movies-", StringComparison.OrdinalIgnoreCase))
                {
                    targetCanonical = "Movies";
                }
                else if (string.Equals(catName, "Drama", StringComparison.OrdinalIgnoreCase) || string.Equals(catName, "General", StringComparison.OrdinalIgnoreCase))
                {
                    targetCanonical = "Movies";
                }

                if (targetCanonical != null && !string.Equals(catName, targetCanonical, StringComparison.OrdinalIgnoreCase))
                {
                    // Find or create destination category
                    var targetCat = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Name.ToLower() == targetCanonical.ToLower(), cancellationToken);
                    if (targetCat == null)
                    {
                        targetCat = new Category { Name = targetCanonical, CreatedAt = DateTime.UtcNow, IsUserCreated = false };
                        _dbContext.Categories.Add(targetCat);
                        await _dbContext.SaveChangesAsync(cancellationToken);
                    }

                    // Reassign all videos in legacy category
                    var videosToMove = cat.Videos.ToList();
                    if (videosToMove.Any())
                    {
                        foreach (var v in videosToMove)
                        {
                            v.CategoryId = targetCat.Id;
                            v.UpdatedAt = DateTime.UtcNow;
                        }
                        result.VideosReassigned += videosToMove.Count;
                        result.Details.Add($"Reassigned {videosToMove.Count} videos from '{catName}' to '{targetCanonical}'.");
                    }

                    // Remove legacy category
                    _dbContext.Categories.Remove(cat);
                    result.CategoriesMerged++;
                    result.Details.Add($"Merged and deleted legacy category '{catName}'.");
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            // 2. Prune empty non-protected categories
            var remainingCategories = await _dbContext.Categories
                .Include(c => c.Videos)
                .Where(c => c.Name != "Downloads" && c.Name != "Radarr")
                .ToListAsync(cancellationToken);

            foreach (var c in remainingCategories)
            {
                var activeVideos = c.Videos.Count(v => v.ScanStatus != ScanState.Missing);
                if (activeVideos == 0)
                {
                    _dbContext.Categories.Remove(c);
                    result.EmptyCategoriesDeleted++;
                    result.Details.Add($"Removed empty category '{c.Name}'.");
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            // 3. Prune empty folders on disk
            if (Directory.Exists(MediaRoot))
            {
                var removedCount = PruneEmptyDirectories(MediaRoot);
                result.EmptyFoldersRemoved = removedCount;
                if (removedCount > 0)
                {
                    result.Details.Add($"Pruned {removedCount} empty folders on disk.");
                }
            }

            _logger.LogInformation("Consolidation finished: Merged {Merged}, Deleted {Deleted}, Reassigned {Reassigned}, Disk Folders Pruned {Pruned}",
                result.CategoriesMerged, result.EmptyCategoriesDeleted, result.VideosReassigned, result.EmptyFoldersRemoved);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error consolidating categories");
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    public async Task<MediaAuditReportDto> GetAuditReportAsync(CancellationToken cancellationToken = default)
    {
        var allVideos = await _dbContext.Videos.Include(v => v.Category).ToListAsync(cancellationToken);
        var organized = allVideos.Where(v => v.IsOrganized).ToList();
        var unclassified = allVideos.Where(v => string.Equals(v.MediaTypeDetected, "Unclassified", StringComparison.OrdinalIgnoreCase) || 
                                               v.Category?.Name == "Unclassified").ToList();

        var usedCats = allVideos
            .Where(v => v.Category != null)
            .Select(v => v.Category!.Name)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        return new MediaAuditReportDto
        {
            TotalFilesScanned = allVideos.Count,
            SuccessfullyClassified = organized.Count,
            RenamedAndMoved = organized.Count,
            Unclassified = unclassified.Count,
            Skipped = allVideos.Count - organized.Count,
            Errors = allVideos.Count(v => v.ScanStatus == ScanState.Failed),
            EmptyFoldersRemoved = 0,
            CategoriesUsed = usedCats
        };
    }

    private async Task<MediaClassificationResult> GetOrClassifyMediaAsync(Video video, CancellationToken cancellationToken)
    {
        // 1. Harvest technical & embedded tags (crucial for non-yt-dlp downloads)
        var evidence = await _metadataHarvester.HarvestAsync(video, cancellationToken);
        var evidencePrompt = evidence.BuildAiEvidencePrompt();

        // If already classified with full details and valid category, reuse existing
        if (!string.IsNullOrWhiteSpace(video.MediaTypeDetected) &&
            !string.IsNullOrWhiteSpace(video.PrimaryGenre) &&
            !string.IsNullOrWhiteSpace(video.Summary) &&
            (ValidVideoCategories.Contains(video.Category?.Name ?? "") || ValidAudioCategories.Contains(video.Category?.Name ?? "")))
        {
            var isAud = evidence.IsAudio || string.Equals(video.MediaTypeDetected, "Audio", StringComparison.OrdinalIgnoreCase);
            return new MediaClassificationResult
            {
                MediaType = isAud ? "Audio" : "Video",
                Category = NormalizeCategory(video.Category?.Name ?? video.PrimaryGenre, isAud),
                CanonicalTitle = video.DisplayTitle,
                Language = video.DetectedLanguage ?? evidence.DetectedLanguage,
                ReleaseYear = video.ReleaseYear ?? evidence.ExtractedYear,
                Artist = video.Uploader ?? evidence.EmbeddedArtist,
                PrimaryGenre = video.PrimaryGenre,
                Summary = video.Summary,
                SuggestedFileName = video.DisplayTitle,
                Confidence = 1.0,
                ClassificationReason = "Reused verified classification metadata."
            };
        }

        // 2. Query OCI SmartRoute with synthesized evidence
        var classification = await _smartRouteClient.ClassifyMediaWithEvidenceAsync(
            video.DisplayTitle,
            video.Uploader ?? evidence.EmbeddedArtist,
            video.RawDescription,
            video.Transcript,
            evidencePrompt,
            cancellationToken);

        if (classification != null && !string.IsNullOrWhiteSpace(classification.CanonicalTitle))
        {
            var isAud = classification.MediaType.Equals("Audio", StringComparison.OrdinalIgnoreCase) || evidence.IsAudio;
            var normalizedCat = NormalizeCategory(classification.Category, isAud);

            // Supplement missing language or year if harvested from embedded tags
            var finalLang = !string.IsNullOrWhiteSpace(classification.Language) && !classification.Language.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
                ? classification.Language
                : evidence.DetectedLanguage;

            var finalYear = classification.ReleaseYear ?? evidence.ExtractedYear;
            var finalArtist = !string.IsNullOrWhiteSpace(classification.Artist)
                ? classification.Artist
                : (!string.IsNullOrWhiteSpace(classification.ArtistOrDirector) ? classification.ArtistOrDirector : evidence.EmbeddedArtist);

            var finalEpisode = !string.IsNullOrWhiteSpace(classification.SeasonEpisode)
                ? classification.SeasonEpisode
                : evidence.ExtractedSeasonEpisode;

            return classification with
            {
                MediaType = isAud ? "Audio" : "Video",
                Category = normalizedCat,
                Language = finalLang,
                ReleaseYear = finalYear,
                Artist = finalArtist,
                ArtistOrDirector = finalArtist,
                SeasonEpisode = finalEpisode
            };
        }

        // 3. Fallback: derive sensible defaults from harvested evidence
        var isAudioFallback = evidence.IsAudio || IsAudioVideo(video);
        var fallbackCategory = isAudioFallback ? "Songs" : "Movies";
        var fallbackTitle = !string.IsNullOrWhiteSpace(evidence.CleanTitleCandidate)
            ? evidence.CleanTitleCandidate
            : CleanTitle(video.DisplayTitle ?? Path.GetFileNameWithoutExtension(video.FileName));

        return new MediaClassificationResult
        {
            MediaType = isAudioFallback ? "Audio" : "Video",
            Category = fallbackCategory,
            CanonicalTitle = fallbackTitle,
            Language = evidence.DetectedLanguage,
            ReleaseYear = evidence.ExtractedYear ?? video.CreatedAt.Year,
            Artist = evidence.EmbeddedArtist ?? video.Uploader,
            ArtistOrDirector = evidence.EmbeddedArtist ?? video.Uploader,
            SeasonEpisode = evidence.ExtractedSeasonEpisode,
            PrimaryGenre = isAudioFallback ? "Music" : "General",
            Summary = $"Media file: {fallbackTitle}",
            SuggestedFileName = fallbackTitle,
            Confidence = 0.5,
            ClassificationReason = "Fallback derived from sanitized filename and embedded tags."
        };
    }

    private (string TargetFileName, string TargetRelativePath) ComputeDestinationPaths(Video video, MediaClassificationResult classification)
    {
        var ext = Path.GetExtension(video.FileName);
        var isAudio = classification.MediaType.Equals("Audio", StringComparison.OrdinalIgnoreCase) || IsAudioVideo(video);

        // Normalize Category into strict allowed categories
        var category = NormalizeCategory(classification.Category, isAudio);

        // Build hyphenated components
        var lang = !string.IsNullOrWhiteSpace(classification.Language) ? ToHyphenated(classification.Language) : null;
        var title = !string.IsNullOrWhiteSpace(classification.CanonicalTitle) ? ToHyphenated(classification.CanonicalTitle) : ToHyphenated(Path.GetFileNameWithoutExtension(video.FileName));
        var year = classification.ReleaseYear.HasValue && classification.ReleaseYear.Value >= 1900 && classification.ReleaseYear.Value <= DateTime.UtcNow.Year + 2
            ? classification.ReleaseYear.Value.ToString()
            : null;
        var artist = !string.IsNullOrWhiteSpace(classification.Artist)
            ? ToHyphenated(classification.Artist)
            : (!string.IsNullOrWhiteSpace(classification.ArtistOrDirector) ? ToHyphenated(classification.ArtistOrDirector) : null);
        var seasonEpisode = !string.IsNullOrWhiteSpace(classification.SeasonEpisode) ? ToHyphenated(classification.SeasonEpisode) : null;
        var part = !string.IsNullOrWhiteSpace(classification.Part) ? ToHyphenated(classification.Part) : null;

        string targetFileName;
        string folderRelative;

        if (isAudio)
        {
            folderRelative = $"Audio/{category}";

            if (category.Equals("Podcasts", StringComparison.OrdinalIgnoreCase))
            {
                // Podcast-Show-Name-S01E01.ext
                var showName = !string.IsNullOrWhiteSpace(artist) ? artist : title;
                var ep = seasonEpisode ?? "S01E01";
                targetFileName = JoinHyphenated("Podcast", showName, ep) + ext;
            }
            else if (category.Equals("Audiobooks", StringComparison.OrdinalIgnoreCase))
            {
                // Audiobook-Author-Book-Title-Part01.ext
                var author = artist ?? "Author";
                var p = part ?? "Part01";
                targetFileName = JoinHyphenated("Audiobook", author, title, p) + ext;
            }
            else if (category.Equals("Songs", StringComparison.OrdinalIgnoreCase))
            {
                // Preferred format: Language-Song-Title-Artist.ext
                // If artist unknown, omit: Language-Song-Title.ext
                targetFileName = JoinHyphenated(lang, title, artist) + ext;
            }
            else
            {
                // Devotional, Classical, Comedy, Soundtracks, Recordings, Unclassified
                // Language-Title-Artist.ext or Language-Title.ext
                targetFileName = JoinHyphenated(lang, title, artist) + ext;
            }
        }
        else
        {
            folderRelative = $"Videos/{category}";

            if (category.Equals("TV Shows", StringComparison.OrdinalIgnoreCase) || category.Equals("Web Series", StringComparison.OrdinalIgnoreCase))
            {
                // Preferred format: Language-Series-Title-S01E01.ext
                var ep = seasonEpisode ?? "S01E01";
                targetFileName = JoinHyphenated(lang, title, ep) + ext;
            }
            else if (category.Equals("Documentaries", StringComparison.OrdinalIgnoreCase) || category.Equals("Short Films", StringComparison.OrdinalIgnoreCase))
            {
                // Preferred format: Language-Title-Year.ext
                targetFileName = JoinHyphenated(lang, title, year) + ext;
            }
            else if (category.Equals("Movies", StringComparison.OrdinalIgnoreCase))
            {
                // Preferred format: Language-Movie-Title-Year.ext
                // Examples: Kannada-Kantara-2022.mkv, English-Interstellar-2014.mkv
                targetFileName = JoinHyphenated(lang, title, year) + ext;
            }
            else
            {
                // Comedy, Educational, Music Videos, Personal Videos, Unclassified
                targetFileName = JoinHyphenated(lang, title, year) + ext;
            }
        }

        var fullRelative = $"{folderRelative}/{targetFileName}";
        return (targetFileName, fullRelative);
    }

    private static string NormalizeCategory(string? rawCategory, bool isAudio)
    {
        if (string.IsNullOrWhiteSpace(rawCategory)) return isAudio ? "Songs" : "Movies";

        var trimmed = rawCategory.Trim();

        if (isAudio)
        {
            if (ValidAudioCategories.Contains(trimmed)) return ValidAudioCategories.First(c => c.Equals(trimmed, StringComparison.OrdinalIgnoreCase));

            if (trimmed.Contains("podcast", StringComparison.OrdinalIgnoreCase)) return "Podcasts";
            if (trimmed.Contains("audiobook", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("book", StringComparison.OrdinalIgnoreCase)) return "Audiobooks";
            if (trimmed.Contains("devotion", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("spiritual", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("bhajan", StringComparison.OrdinalIgnoreCase)) return "Devotional";
            if (trimmed.Contains("classic", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("carnatic", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("hindustani", StringComparison.OrdinalIgnoreCase)) return "Classical";
            if (trimmed.Contains("soundtrack", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("score", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("ost", StringComparison.OrdinalIgnoreCase)) return "Soundtracks";
            if (trimmed.Contains("comedy", StringComparison.OrdinalIgnoreCase)) return "Comedy";
            if (trimmed.Contains("record", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("voice", StringComparison.OrdinalIgnoreCase)) return "Recordings";
            if (trimmed.Contains("song", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("music", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("folk", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("pop", StringComparison.OrdinalIgnoreCase)) return "Songs";

            return "Songs";
        }
        else
        {
            if (ValidVideoCategories.Contains(trimmed)) return ValidVideoCategories.First(c => c.Equals(trimmed, StringComparison.OrdinalIgnoreCase));

            if (trimmed.Contains("movie", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("film", StringComparison.OrdinalIgnoreCase)) return "Movies";
            if (trimmed.Contains("tv", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("show", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("television", StringComparison.OrdinalIgnoreCase)) return "TV Shows";
            if (trimmed.Contains("web", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("series", StringComparison.OrdinalIgnoreCase)) return "Web Series";
            if (trimmed.Contains("docu", StringComparison.OrdinalIgnoreCase)) return "Documentaries";
            if (trimmed.Contains("short", StringComparison.OrdinalIgnoreCase)) return "Short Films";
            if (trimmed.Contains("comedy", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("standup", StringComparison.OrdinalIgnoreCase)) return "Comedy";
            if (trimmed.Contains("educat", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("tutorial", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("lecture", StringComparison.OrdinalIgnoreCase)) return "Educational";
            if (trimmed.Contains("music video", StringComparison.OrdinalIgnoreCase)) return "Music Videos";
            if (trimmed.Contains("personal", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("home", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("vlog", StringComparison.OrdinalIgnoreCase)) return "Personal Videos";

            return "Movies";
        }
    }

    public static string ToHyphenated(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var invalidChars = Path.GetInvalidFileNameChars().Concat(new[] { ':', '*', '?', '"', '<', '>', '|', '/', '\\' }).Distinct();
        var clean = string.Concat(text.Select(c => invalidChars.Contains(c) ? '-' : c));

        // Replace whitespace, underscores, dots with hyphens
        clean = Regex.Replace(clean, @"[\s_\.]+", "-");

        // Collapse repeated hyphens
        clean = Regex.Replace(clean, @"-+", "-");

        return clean.Trim('-');
    }

    public static string JoinHyphenated(params string?[] parts)
    {
        var nonNullParts = parts
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!.Trim('-'))
            .Where(p => p.Length > 0);

        var joined = string.Join("-", nonNullParts);
        return Regex.Replace(joined, @"-+", "-").Trim('-');
    }

    private static void MoveCompanionSubtitles(string sourceDir, string oldBaseName, string targetDir, string newBaseName)
    {
        try
        {
            var files = Directory.GetFiles(sourceDir, $"{oldBaseName}*.*");
            foreach (var file in files)
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext is ".vtt" or ".srt" or ".sub" or ".idx")
                {
                    var fileNoExt = Path.GetFileNameWithoutExtension(file);
                    var langSuffix = "";
                    var dotIdx = fileNoExt.LastIndexOf('.');
                    if (dotIdx >= 0)
                    {
                        langSuffix = fileNoExt.Substring(dotIdx);
                    }

                    var targetSubName = $"{newBaseName}{langSuffix}{ext}";
                    var targetSubPath = Path.Combine(targetDir, targetSubName);
                    File.Move(file, targetSubPath, overwrite: true);
                }
                else if (ext is ".jpg" or ".png" or ".webp")
                {
                    var targetPosterPath = Path.Combine(targetDir, "poster" + ext);
                    File.Copy(file, targetPosterPath, overwrite: true);
                }
            }
        }
        catch { }
    }

    private static async Task GenerateNfoFileAsync(string targetDir, string targetFileName, MediaClassificationResult classification, CancellationToken cancellationToken)
    {
        try
        {
            var nfoPath = Path.Combine(targetDir, Path.GetFileNameWithoutExtension(targetFileName) + ".nfo");
            var isMovie = classification.MediaType.Equals("Video", StringComparison.OrdinalIgnoreCase) || classification.MediaType.Equals("Movie", StringComparison.OrdinalIgnoreCase);

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\" ?>");
            sb.AppendLine(isMovie ? "<movie>" : "<musicvideo>");
            sb.AppendLine($"    <title>{EscapeXml(classification.CanonicalTitle)}</title>");
            if (!string.IsNullOrWhiteSpace(classification.Language)) sb.AppendLine($"    <language>{EscapeXml(classification.Language)}</language>");
            if (classification.ReleaseYear.HasValue) sb.AppendLine($"    <year>{classification.ReleaseYear.Value}</year>");
            if (!string.IsNullOrWhiteSpace(classification.Summary)) sb.AppendLine($"    <plot>{EscapeXml(classification.Summary)}</plot>");
            if (!string.IsNullOrWhiteSpace(classification.Category)) sb.AppendLine($"    <category>{EscapeXml(classification.Category)}</category>");
            if (!string.IsNullOrWhiteSpace(classification.PrimaryGenre)) sb.AppendLine($"    <genre>{EscapeXml(classification.PrimaryGenre)}</genre>");
            var artist = classification.Artist ?? classification.ArtistOrDirector;
            if (!string.IsNullOrWhiteSpace(artist))
            {
                sb.AppendLine(isMovie
                    ? $"    <director>{EscapeXml(artist)}</director>"
                    : $"    <artist>{EscapeXml(artist)}</artist>");
            }
            sb.AppendLine(isMovie ? "</movie>" : "</musicvideo>");

            await File.WriteAllTextAsync(nfoPath, sb.ToString(), cancellationToken);
        }
        catch { }
    }

    private static string AvoidCollision(string targetFullPath, string currentSourcePath)
    {
        if (!File.Exists(targetFullPath) || string.Equals(targetFullPath, currentSourcePath, StringComparison.OrdinalIgnoreCase))
        {
            return targetFullPath;
        }

        var dir = Path.GetDirectoryName(targetFullPath)!;
        var nameNoExt = Path.GetFileNameWithoutExtension(targetFullPath);
        var ext = Path.GetExtension(targetFullPath);
        int counter = 1;

        while (File.Exists(targetFullPath))
        {
            var candidate = Path.Combine(dir, $"{nameNoExt}-{counter}{ext}");
            if (!File.Exists(candidate) || string.Equals(candidate, currentSourcePath, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
            counter++;
        }

        return targetFullPath;
    }

    private string GetRelativePathFromMediaRoot(string fullPath)
    {
        if (fullPath.StartsWith(MediaRoot, StringComparison.OrdinalIgnoreCase))
        {
            var rel = fullPath.Substring(MediaRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return rel.Replace(Path.DirectorySeparatorChar, '/');
        }
        return Path.GetFileName(fullPath);
    }

    private void CleanEmptyParentDirectories(string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return;

        try
        {
            var rootUri = new Uri(MediaRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar);
            var currentUri = new Uri(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar);

            if (rootUri.IsBaseOf(currentUri) && !rootUri.Equals(currentUri))
            {
                var entries = Directory.GetFileSystemEntries(dir);
                if (entries.Length == 0)
                {
                    Directory.Delete(dir);
                    CleanEmptyParentDirectories(Path.GetDirectoryName(dir));
                }
            }
        }
        catch { }
    }

    private static int PruneEmptyDirectories(string rootPath)
    {
        int pruned = 0;
        try
        {
            foreach (var dir in Directory.GetDirectories(rootPath, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
            {
                try
                {
                    if (Directory.Exists(dir))
                    {
                        var entries = Directory.GetFileSystemEntries(dir);
                        if (entries.Length == 0)
                        {
                            Directory.Delete(dir);
                            pruned++;
                        }
                    }
                }
                catch { }
            }
        }
        catch { }
        return pruned;
    }

    private static bool IsAudioVideo(Video v)
    {
        return MediaMetadataHarvester.IsAudioFile(v.FileName, v.Height);
    }

    private static string CleanTitle(string title)
    {
        var clean = Regex.Replace(title, @"\b(official video|official audio|4k|1080p|hd|remastered|lyrics|music video)\b", "", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"[\[\(\{].*?[\]\)\}]", "").Trim();
        return string.IsNullOrWhiteSpace(clean) ? title : clean;
    }

    private static string EscapeXml(string text)
    {
        return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
    }

    private class NullMediaToolRunner : MyTube.Backend.Application.IMediaToolRunner
    {
        public string GetYtDlpPath() => "yt-dlp";
        public string GetFfmpegPath() => "ffmpeg";
        public string GetFfprobePath() => "ffprobe";

        public Task<MyTube.Backend.Application.ToolExecutionResult> RunToolAsync(
            string toolName,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken = default,
            TimeSpan? timeout = null,
            Action<string>? onStandardOutputLine = null,
            Action<string>? onStandardErrorLine = null,
            string? workingDirectory = null)
        {
            return Task.FromResult(new MyTube.Backend.Application.ToolExecutionResult(-1, string.Empty, string.Empty));
        }
    }
}
