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
using MyTube.Backend.Application.FileNaming;
using MyTube.Backend.Domain;
using MyTube.Backend.Infrastructure.Media;

namespace MyTube.Backend.Infrastructure.FileNaming;

public class MediaNamingService : IMediaNamingService
{
    private readonly VideoDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MediaNamingService> _logger;
    private FileNamingSettingsDto _settings;

    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public MediaNamingService(
        VideoDbContext dbContext,
        IConfiguration configuration,
        ILogger<MediaNamingService> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;

        _settings = new FileNamingSettingsDto
        {
            Pattern = configuration["Media:Naming:Pattern"] ?? "{Title} [{Quality}] [{VideoCodec}]",
            SpaceReplacement = Enum.TryParse<SpaceReplacementOption>(configuration["Media:Naming:SpaceReplacement"], out var sr) ? sr : SpaceReplacementOption.Space,
            ColonReplacement = Enum.TryParse<ColonReplacementOption>(configuration["Media:Naming:ColonReplacement"], out var cr) ? cr : ColonReplacementOption.Dash,
            AutoOrganizeIntoCategoryFolders = bool.TryParse(configuration["Media:Naming:AutoOrganizeIntoCategoryFolders"], out var ao) && ao,
            MoveToRecycleBinOnDelete = !bool.TryParse(configuration["Media:Naming:MoveToRecycleBinOnDelete"], out var rb) || rb
        };
    }

    private string MediaRoot => _configuration["Media:RootPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "media");
    private string RecycleBinPath => Path.Combine(MediaRoot, ".recycle");

    public FileNamingSettingsDto GetSettings() => _settings;

    public async Task UpdateSettingsAsync(FileNamingSettingsDto settings, CancellationToken cancellationToken = default)
    {
        _settings.Pattern = string.IsNullOrWhiteSpace(settings.Pattern) ? "{Title} [{Quality}] [{VideoCodec}]" : settings.Pattern.Trim();
        _settings.SpaceReplacement = settings.SpaceReplacement;
        _settings.ColonReplacement = settings.ColonReplacement;
        _settings.AutoOrganizeIntoCategoryFolders = settings.AutoOrganizeIntoCategoryFolders;
        _settings.MoveToRecycleBinOnDelete = settings.MoveToRecycleBinOnDelete;

        try
        {
            var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(appSettingsPath))
            {
                var json = await File.ReadAllTextAsync(appSettingsPath, cancellationToken);
                var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json, _jsonOptions) ?? new();
                dict["Media:Naming:Pattern"] = _settings.Pattern;
                dict["Media:Naming:SpaceReplacement"] = _settings.SpaceReplacement.ToString();
                dict["Media:Naming:ColonReplacement"] = _settings.ColonReplacement.ToString();
                dict["Media:Naming:AutoOrganizeIntoCategoryFolders"] = _settings.AutoOrganizeIntoCategoryFolders.ToString();
                dict["Media:Naming:MoveToRecycleBinOnDelete"] = _settings.MoveToRecycleBinOnDelete.ToString();

                await File.WriteAllTextAsync(appSettingsPath, JsonSerializer.Serialize(dict, _jsonOptions), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist naming settings to appsettings.json. Settings kept in-memory.");
        }
    }

    public string FormatFileName(Video video, string pattern, SpaceReplacementOption spaceReplacement, ColonReplacementOption colonReplacement)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            pattern = "{Title} [{Quality}] [{VideoCodec}]";
        }

        var title = !string.IsNullOrWhiteSpace(video.DisplayTitle)
            ? video.DisplayTitle
            : Path.GetFileNameWithoutExtension(video.FileName);

        var cleanTitle = CleanTitleString(title);
        var origFileName = Path.GetFileNameWithoutExtension(video.FileName);
        var categoryName = video.Category?.Name ?? "Uncategorized";
        var year = (video.LastModified != default ? video.LastModified.Year : video.CreatedAt.Year).ToString();
        var date = (video.LastModified != default ? video.LastModified : video.CreatedAt).ToString("yyyy-MM-dd");

        string quality = video.Height switch
        {
            >= 2160 => "4K",
            >= 1440 => "2K",
            >= 1080 => "1080p",
            >= 720 => "720p",
            >= 480 => "480p",
            >= 360 => "360p",
            > 0 => $"{video.Height}p",
            _ => (video.FileName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) || video.FileName.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase)) ? "Audio" : "SD"
        };

        var resolution = video.Width > 0 && video.Height > 0 ? $"{video.Width}x{video.Height}" : quality;
        var videoCodec = NormalizeCodec(video.VideoCodec, "x264");
        var audioCodec = NormalizeCodec(video.AudioCodec, "aac");

        // Handle colon replacement in title
        title = ApplyColonReplacement(title, colonReplacement);
        cleanTitle = ApplyColonReplacement(cleanTitle, colonReplacement);

        // Replace tokens
        var result = pattern;
        result = ReplaceTokenCaseInsensitive(result, "{Title}", title);
        result = ReplaceTokenCaseInsensitive(result, "{CleanTitle}", cleanTitle);
        result = ReplaceTokenCaseInsensitive(result, "{OriginalFilename}", origFileName);
        result = ReplaceTokenCaseInsensitive(result, "{Category}", categoryName);
        result = ReplaceTokenCaseInsensitive(result, "{Year}", year);
        result = ReplaceTokenCaseInsensitive(result, "{Date}", date);
        result = ReplaceTokenCaseInsensitive(result, "{Quality}", quality);
        result = ReplaceTokenCaseInsensitive(result, "{Resolution}", resolution);
        result = ReplaceTokenCaseInsensitive(result, "{VideoCodec}", videoCodec);
        result = ReplaceTokenCaseInsensitive(result, "{AudioCodec}", audioCodec);

        // Remove empty brackets like [] or ()
        result = Regex.Replace(result, @"\[\s*\]|\(\s*\)", "").Trim();

        // Apply space replacement
        result = ApplySpaceReplacement(result, spaceReplacement);

        // Sanitize path separators & file name
        return SanitizeFormattedResult(result);
    }

    public async Task<FileRenamePreviewResultDto> PreviewRenamingAsync(FileRenamePreviewRequest request, CancellationToken cancellationToken = default)
    {
        var pattern = !string.IsNullOrWhiteSpace(request.Pattern) ? request.Pattern : _settings.Pattern;
        var spaceOption = request.SpaceReplacement;
        var colonOption = request.ColonReplacement;
        var autoOrganize = request.AutoOrganizeIntoCategoryFolders;

        var query = _dbContext.Videos.Include(v => v.Category).AsQueryable();

        if (request.VideoIds != null && request.VideoIds.Count > 0)
        {
            query = query.Where(v => request.VideoIds.Contains(v.Id));
        }

        var videos = await query.ToListAsync(cancellationToken);
        var previewItems = new List<FileRenameItemPreviewDto>();
        var seenTargetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var v in videos)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentExt = Path.GetExtension(v.FileName);
            var formattedBase = FormatFileName(v, pattern, spaceOption, colonOption);

            string targetRelativeDir = "";
            string targetFileName = formattedBase;

            // If pattern contains slash or auto-organize is enabled
            if (formattedBase.Contains('/') || formattedBase.Contains('\\'))
            {
                var parts = formattedBase.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                targetFileName = parts.Last();
                targetRelativeDir = string.Join(Path.DirectorySeparatorChar.ToString(), parts.Take(parts.Length - 1));
            }
            else if (autoOrganize && v.Category != null && !string.IsNullOrWhiteSpace(v.Category.Name))
            {
                targetRelativeDir = CleanFolderString(v.Category.Name);
            }

            var cleanTargetName = SanitizeFileName(targetFileName) + currentExt;
            var newRelativePath = string.IsNullOrEmpty(targetRelativeDir)
                ? cleanTargetName
                : Path.Combine(targetRelativeDir, cleanTargetName).Replace('\\', '/');

            // Current relative path
            var currentRelativePath = string.Empty;
            if (!string.IsNullOrEmpty(v.FilePath))
            {
                try
                {
                    currentRelativePath = Path.GetRelativePath(MediaRoot, v.FilePath).Replace('\\', '/');
                }
                catch
                {
                    currentRelativePath = v.FileName;
                }
            }
            else
            {
                currentRelativePath = v.FileName;
            }

            bool willChange = !string.Equals(currentRelativePath, newRelativePath, StringComparison.OrdinalIgnoreCase);
            bool hasConflict = false;
            string? conflictReason = null;

            if (willChange)
            {
                if (seenTargetPaths.Contains(newRelativePath))
                {
                    hasConflict = true;
                    conflictReason = "Naming collision with another video in this batch.";
                }
                else
                {
                    seenTargetPaths.Add(newRelativePath);

                    var proposedFullDiskPath = Path.Combine(MediaRoot, newRelativePath.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(proposedFullDiskPath) && !string.Equals(proposedFullDiskPath, v.FilePath, StringComparison.OrdinalIgnoreCase))
                    {
                        hasConflict = true;
                        conflictReason = "A different file already exists at the target destination on disk.";
                    }
                }
            }

            previewItems.Add(new FileRenameItemPreviewDto
            {
                VideoId = v.Id,
                CurrentTitle = v.DisplayTitle,
                CurrentFileName = v.FileName,
                CurrentRelativePath = currentRelativePath,
                NewFileName = cleanTargetName,
                NewRelativePath = newRelativePath,
                CategoryName = v.Category?.Name ?? "Uncategorized",
                Quality = v.Height > 0 ? $"{v.Height}p" : "SD",
                VideoCodec = v.VideoCodec,
                WillChange = willChange,
                HasConflict = hasConflict,
                ConflictReason = conflictReason
            });
        }

        return new FileRenamePreviewResultDto
        {
            Pattern = pattern,
            TotalVideos = videos.Count,
            VideosToRename = previewItems.Count(p => p.WillChange && !p.HasConflict),
            ConflictsCount = previewItems.Count(p => p.HasConflict),
            Items = previewItems
        };
    }

    public async Task<FileRenameExecuteResultDto> ExecuteRenamingAsync(FileRenameExecuteRequest request, CancellationToken cancellationToken = default)
    {
        var preview = await PreviewRenamingAsync(new FileRenamePreviewRequest
        {
            Pattern = request.Pattern,
            SpaceReplacement = request.SpaceReplacement,
            ColonReplacement = request.ColonReplacement,
            AutoOrganizeIntoCategoryFolders = request.AutoOrganizeIntoCategoryFolders,
            VideoIds = request.VideoIds
        }, cancellationToken);

        var result = new FileRenameExecuteResultDto();
        var videoIdsToRename = preview.Items.Where(i => i.WillChange && !i.HasConflict).Select(i => i.VideoId).ToList();

        if (videoIdsToRename.Count == 0)
        {
            result.Success = true;
            result.SkippedCount = preview.Items.Count;
            result.Messages.Add("No files needed renaming or all items had conflicts.");
            return result;
        }

        var videos = await _dbContext.Videos
            .Include(v => v.Category)
            .Where(v => videoIdsToRename.Contains(v.Id))
            .ToListAsync(cancellationToken);

        var itemMap = preview.Items.ToDictionary(i => i.VideoId);
        var oldDirsToClean = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Force GC release on file streams before moving files
        GC.Collect();
        GC.WaitForPendingFinalizers();

        foreach (var v in videos)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!itemMap.TryGetValue(v.Id, out var item)) continue;

            try
            {
                var oldFilePath = v.FilePath;
                var newRelativePath = item.NewRelativePath.Replace('/', Path.DirectorySeparatorChar);
                var newFullFilePath = Path.Combine(MediaRoot, newRelativePath);

                if (File.Exists(oldFilePath))
                {
                    var oldDir = Path.GetDirectoryName(oldFilePath);
                    if (!string.IsNullOrEmpty(oldDir)) oldDirsToClean.Add(oldDir);

                    var newDir = Path.GetDirectoryName(newFullFilePath);
                    if (!string.IsNullOrEmpty(newDir) && !Directory.Exists(newDir))
                    {
                        Directory.CreateDirectory(newDir);
                    }

                    // Move video file
                    File.Move(oldFilePath, newFullFilePath, overwrite: true);

                    // Move associated thumbnail if present
                    if (!string.IsNullOrEmpty(v.ThumbnailPath) && File.Exists(v.ThumbnailPath))
                    {
                        try
                        {
                            var thumbExt = Path.GetExtension(v.ThumbnailPath);
                            var thumbDir = Path.GetDirectoryName(v.ThumbnailPath);
                            var newThumbName = Path.GetFileNameWithoutExtension(item.NewFileName) + thumbExt;
                            var newThumbPath = Path.Combine(thumbDir ?? string.Empty, newThumbName);

                            if (v.ThumbnailPath != newThumbPath)
                            {
                                File.Move(v.ThumbnailPath, newThumbPath, overwrite: true);
                                v.ThumbnailPath = newThumbPath;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, $"Failed to rename thumbnail for video {v.Id}");
                        }
                    }

                    // Update Video Entity
                    v.FilePath = newFullFilePath;
                    v.FileName = item.NewFileName;
                    v.UpdatedAt = DateTime.UtcNow;

                    result.RenamedCount++;
                }
                else
                {
                    result.SkippedCount++;
                    result.Messages.Add($"Source file missing on disk for '{v.DisplayTitle}'");
                }
            }
            catch (Exception ex)
            {
                result.ErrorCount++;
                result.Messages.Add($"Error renaming '{v.DisplayTitle}': {ex.Message}");
                _logger.LogError(ex, $"Error renaming video {v.Id}");
            }
        }

        if (result.RenamedCount > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // Clean up empty old directories
        foreach (var dir in oldDirsToClean)
        {
            try
            {
                if (Directory.Exists(dir) &&
                    !string.Equals(dir.TrimEnd('/', '\\'), MediaRoot.TrimEnd('/', '\\'), StringComparison.OrdinalIgnoreCase))
                {
                    if (!Directory.EnumerateFileSystemEntries(dir).Any())
                    {
                        Directory.Delete(dir, recursive: false);
                    }
                }
            }
            catch { }
        }

        result.Success = result.ErrorCount == 0;
        result.Messages.Insert(0, $"Successfully renamed {result.RenamedCount} video files on disk.");
        return result;
    }

    public async Task<RecycleBinSummaryDto> GetRecycleBinSummaryAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var summary = new RecycleBinSummaryDto
            {
                RecycleBinPath = RecycleBinPath
            };

            if (!Directory.Exists(RecycleBinPath))
            {
                return summary;
            }

            var files = Directory.GetFiles(RecycleBinPath, "*", SearchOption.AllDirectories);
            long totalBytes = 0;

            foreach (var f in files)
            {
                try
                {
                    var fi = new FileInfo(f);
                    totalBytes += fi.Length;
                    var relPath = Path.GetRelativePath(RecycleBinPath, f).Replace('\\', '/');

                    summary.Files.Add(new RecycledFileItemDto
                    {
                        FileName = fi.Name,
                        OriginalRelativePath = relPath,
                        FileSizeBytes = fi.Length,
                        FormattedSize = FormatBytes(fi.Length),
                        DeletedAt = fi.LastWriteTimeUtc
                    });
                }
                catch { }
            }

            summary.FileCount = summary.Files.Count;
            summary.TotalSizeBytes = totalBytes;
            summary.FormattedTotalSize = FormatBytes(totalBytes);
            summary.Files = summary.Files.OrderByDescending(f => f.DeletedAt).ToList();

            return summary;
        }, cancellationToken);
    }

    public async Task<int> EmptyRecycleBinAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            if (!Directory.Exists(RecycleBinPath)) return 0;

            int deletedCount = 0;
            var files = Directory.GetFiles(RecycleBinPath, "*", SearchOption.AllDirectories);
            foreach (var f in files)
            {
                try
                {
                    File.SetAttributes(f, FileAttributes.Normal);
                    File.Delete(f);
                    deletedCount++;
                }
                catch { }
            }

            // Remove empty subdirectories
            foreach (var d in Directory.GetDirectories(RecycleBinPath, "*", SearchOption.AllDirectories))
            {
                try
                {
                    if (Directory.Exists(d) && !Directory.EnumerateFileSystemEntries(d).Any())
                    {
                        Directory.Delete(d, true);
                    }
                }
                catch { }
            }

            return deletedCount;
        }, cancellationToken);
    }

    #region Helpers
    private static string CleanTitleString(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "Video";
        // Clean bracketed scene release noise e.g. [1080p], [YTS.LT], (2024), etc. if desired, but keep main title
        var cleaned = Regex.Replace(title, @"\.(mp4|mkv|avi|mov|flv|webm)$", "", RegexOptions.IgnoreCase);
        // Replace dots and underscores with spaces for cleaner token base
        cleaned = cleaned.Replace('.', ' ').Replace('_', ' ');
        // Collapse multiple spaces
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        return string.IsNullOrEmpty(cleaned) ? "Video" : cleaned;
    }

    private static string ApplyColonReplacement(string input, ColonReplacementOption option)
    {
        return option switch
        {
            ColonReplacementOption.Delete => input.Replace(":", ""),
            ColonReplacementOption.Dash => input.Replace(":", " -"),
            ColonReplacementOption.SpaceDashSpace => input.Replace(":", " - "),
            _ => input.Replace(":", " -")
        };
    }

    private static string ApplySpaceReplacement(string input, SpaceReplacementOption option)
    {
        return option switch
        {
            SpaceReplacementOption.Dot => input.Replace(' ', '.'),
            SpaceReplacementOption.Dash => input.Replace(' ', '-'),
            SpaceReplacementOption.Underscore => input.Replace(' ', '_'),
            _ => input
        };
    }

    private static string ReplaceTokenCaseInsensitive(string input, string token, string replacement)
    {
        return Regex.Replace(input, Regex.Escape(token), replacement, RegexOptions.IgnoreCase);
    }

    private static string NormalizeCodec(string? codec, string fallback)
    {
        if (string.IsNullOrWhiteSpace(codec)) return fallback;
        var lower = codec.ToLower().Trim();
        if (lower.Contains("264") || lower.Contains("avc")) return "h264";
        if (lower.Contains("265") || lower.Contains("hevc")) return "hevc";
        if (lower.Contains("av1")) return "av1";
        if (lower.Contains("vp9")) return "vp9";
        if (lower.Contains("aac")) return "aac";
        if (lower.Contains("mp3")) return "mp3";
        if (lower.Contains("ac3")) return "ac3";
        if (lower.Contains("opus")) return "opus";
        return lower;
    }

    private static string CleanFolderString(string folder)
    {
        return SafePathHelper.SanitizeCategoryName(folder, SafePathHelper.DefaultMaxDirectoryBytes, "Media");
    }

    private static string SanitizeFileName(string fileName)
    {
        return SafePathHelper.SanitizeFileName(fileName, SafePathHelper.DefaultMaxStemBytes, "Video");
    }

    private static string SanitizeFormattedResult(string input)
    {
        // Replace invalid chars but preserve forward/backward slashes for directory nesting
        var invalid = Path.GetInvalidPathChars();
        var sb = new StringBuilder();
        foreach (var c in input)
        {
            if (!invalid.Contains(c) && c != '?' && c != '*' && c != '"' && c != '<' && c != '>' && c != '|')
            {
                sb.Append(c);
            }
        }
        var sanitized = sb.ToString().Trim();

        var separators = new[] { '/', '\\' };
        var parts = sanitized.Split(separators, StringSplitOptions.None);
        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (string.IsNullOrWhiteSpace(part)) continue;

            if (i == parts.Length - 1)
            {
                var stem = Path.GetFileNameWithoutExtension(part);
                var ext = Path.GetExtension(part);
                parts[i] = SafePathHelper.TruncateByUtf8Bytes(stem, SafePathHelper.DefaultMaxStemBytes, "video") + ext;
            }
            else
            {
                parts[i] = SafePathHelper.TruncateByUtf8Bytes(part, SafePathHelper.DefaultMaxDirectoryBytes, "folder");
            }
        }

        return string.Join("/", parts);
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }
    #endregion
}
