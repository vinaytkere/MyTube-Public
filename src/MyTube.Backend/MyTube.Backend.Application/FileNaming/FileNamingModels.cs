using System;
using System.Collections.Generic;

namespace MyTube.Backend.Application.FileNaming;

public enum SpaceReplacementOption
{
    Space = 0,
    Dot = 1,
    Dash = 2,
    Underscore = 3
}

public enum ColonReplacementOption
{
    Delete = 0,
    Dash = 1,
    SpaceDashSpace = 2
}

public class FileNamingSettingsDto
{
    public string Pattern { get; set; } = "{Title} [{Quality}] [{VideoCodec}]";
    public SpaceReplacementOption SpaceReplacement { get; set; } = SpaceReplacementOption.Space;
    public ColonReplacementOption ColonReplacement { get; set; } = ColonReplacementOption.Dash;
    public bool AutoOrganizeIntoCategoryFolders { get; set; } = false;
    public bool MoveToRecycleBinOnDelete { get; set; } = true;
}

public class FileRenamePreviewRequest
{
    public string? Pattern { get; set; }
    public SpaceReplacementOption SpaceReplacement { get; set; } = SpaceReplacementOption.Space;
    public ColonReplacementOption ColonReplacement { get; set; } = ColonReplacementOption.Dash;
    public bool AutoOrganizeIntoCategoryFolders { get; set; } = false;
    public List<Guid>? VideoIds { get; set; }
}

public class FileRenameItemPreviewDto
{
    public Guid VideoId { get; set; }
    public string CurrentTitle { get; set; } = string.Empty;
    public string CurrentFileName { get; set; } = string.Empty;
    public string CurrentRelativePath { get; set; } = string.Empty;
    public string NewFileName { get; set; } = string.Empty;
    public string NewRelativePath { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;
    public string VideoCodec { get; set; } = string.Empty;
    public bool WillChange { get; set; }
    public bool HasConflict { get; set; }
    public string? ConflictReason { get; set; }
}

public class FileRenamePreviewResultDto
{
    public string Pattern { get; set; } = string.Empty;
    public int TotalVideos { get; set; }
    public int VideosToRename { get; set; }
    public int ConflictsCount { get; set; }
    public List<FileRenameItemPreviewDto> Items { get; set; } = new();
}

public class FileRenameExecuteRequest
{
    public string? Pattern { get; set; }
    public SpaceReplacementOption SpaceReplacement { get; set; } = SpaceReplacementOption.Space;
    public ColonReplacementOption ColonReplacement { get; set; } = ColonReplacementOption.Dash;
    public bool AutoOrganizeIntoCategoryFolders { get; set; } = false;
    public List<Guid>? VideoIds { get; set; }
}

public class FileRenameExecuteResultDto
{
    public bool Success { get; set; }
    public int RenamedCount { get; set; }
    public int SkippedCount { get; set; }
    public int ErrorCount { get; set; }
    public List<string> Messages { get; set; } = new();
}

public class RecycleBinSummaryDto
{
    public int FileCount { get; set; }
    public long TotalSizeBytes { get; set; }
    public string FormattedTotalSize { get; set; } = "0 B";
    public string RecycleBinPath { get; set; } = string.Empty;
    public List<RecycledFileItemDto> Files { get; set; } = new();
}

public class RecycledFileItemDto
{
    public string FileName { get; set; } = string.Empty;
    public string OriginalRelativePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string FormattedSize { get; set; } = string.Empty;
    public DateTime DeletedAt { get; set; }
}
