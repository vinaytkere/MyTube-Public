using System;
using System.Collections.Generic;

namespace MyTube.Backend.Application.MediaOrganizer.Dtos;

public class MediaOrganizeResultDto
{
    public Guid VideoId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string NewFileName { get; set; } = string.Empty;
    public string OriginalPath { get; set; } = string.Empty;
    public string NewPath { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty; // Video or Audio
    public string Category { get; set; } = string.Empty; // e.g. Movies, Songs, Classical, Devotional
    public string? Language { get; set; }
    public string PrimaryGenre { get; set; } = string.Empty;
    public int? ReleaseYear { get; set; }
    public string? Artist { get; set; }
    public string Summary { get; set; } = string.Empty;
    public double Confidence { get; set; } = 1.0;
    public string ClassificationReason { get; set; } = string.Empty;
    public string ActionTaken { get; set; } = "RenamedAndMoved"; // RenamedAndMoved, Skipped, Error
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class MediaRenamePreviewDto
{
    public Guid VideoId { get; set; }
    public string CurrentTitle { get; set; } = string.Empty;
    public string CurrentFileName { get; set; } = string.Empty;
    public string CurrentRelativePath { get; set; } = string.Empty;
    public string ProposedTitle { get; set; } = string.Empty;
    public string ProposedFileName { get; set; } = string.Empty;
    public string ProposedRelativePath { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Language { get; set; }
    public string PrimaryGenre { get; set; } = string.Empty;
    public int? ReleaseYear { get; set; }
    public string? Artist { get; set; }
    public string Summary { get; set; } = string.Empty;
    public double Confidence { get; set; } = 1.0;
    public string ClassificationReason { get; set; } = string.Empty;
    public bool WillChange { get; set; }
    public bool HasConflict { get; set; }
    public string? ConflictReason { get; set; }
}

public class MediaOrganizerSettingsDto
{
    public bool AutoOrganizeOnDownloadComplete { get; set; } = true;
    public string MovieFolderPattern { get; set; } = "Videos/Movies";
    public string AudioFolderPattern { get; set; } = "Audio/Songs";
    public string MovieFilePattern { get; set; } = "{Language}-{Title}-{Year}";
    public string AudioFilePattern { get; set; } = "{Language}-{Title}-{Artist}";
    public bool GenerateNfoFile { get; set; } = true;
    public bool MoveSubtitles { get; set; } = true;
}

public class CategoryConsolidationResultDto
{
    public int CategoriesMerged { get; set; }
    public int EmptyCategoriesDeleted { get; set; }
    public int VideosReassigned { get; set; }
    public int EmptyFoldersRemoved { get; set; }
    public List<string> Details { get; set; } = new();
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
}

public class MediaAuditReportDto
{
    public int TotalFilesScanned { get; set; }
    public int SuccessfullyClassified { get; set; }
    public int RenamedAndMoved { get; set; }
    public int Unclassified { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }
    public int EmptyFoldersRemoved { get; set; }
    public List<string> CategoriesUsed { get; set; } = new();
}
