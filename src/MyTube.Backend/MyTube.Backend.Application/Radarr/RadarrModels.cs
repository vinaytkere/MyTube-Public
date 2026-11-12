using System;
using System.Collections.Generic;

namespace MyTube.Backend.Application.Radarr;

public class RadarrStatusDto
{
    public bool IsConfigured { get; set; }
    public bool IsConnected { get; set; }
    public string? Version { get; set; }
    public string? AppName { get; set; }
    public string? ErrorMessage { get; set; }
}

public class RadarrFileItemDto
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string FormattedSize { get; set; } = string.Empty;
    public string FileType { get; set; } = "other"; // "video", "pdf", "image", "subtitle", "document", "other"
    public string DownloadUrl { get; set; } = string.Empty;
}

public class RadarrMovieDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? OriginalTitle { get; set; }
    public int Year { get; set; }
    public int TmdbId { get; set; }
    public string? ImdbId { get; set; }
    public string? Overview { get; set; }
    public string? PosterUrl { get; set; }
    public string? FanartUrl { get; set; }
    public List<string> Genres { get; set; } = new();
    public int Runtime { get; set; }
    public double? RatingsValue { get; set; }
    public bool HasFile { get; set; }
    public bool Monitored { get; set; }
    public long? SizeOnDisk { get; set; }
    public string? MovieFilePath { get; set; }
    public int? MovieFileId { get; set; }
    public string? MovieFolderPath { get; set; }
    public string? Quality { get; set; }
    public string? VideoCodec { get; set; }
    public string? AudioFormat { get; set; }
    public string? DownloadUrl { get; set; }
    public string? PosterDownloadUrl { get; set; }
    public string? FanartDownloadUrl { get; set; }

    // All downloaded files (videos, PDFs, images, subtitles, text files, extras)
    public List<RadarrFileItemDto> ExtraFiles { get; set; } = new();

    // Homelab Server Availability Cross-Check
    public bool IsAvailableInHomelab { get; set; }
    public Guid? LocalVideoId { get; set; }
    public string? LocalVideoTitle { get; set; }
    public string? LocalVideoPath { get; set; }
}

public class RadarrDownloadSummaryDto
{
    public int TotalDownloadedCount { get; set; }
    public long TotalSizeBytes { get; set; }
    public string FormattedTotalSize { get; set; } = "0 B";
    public string NonDeletableFolderPath { get; set; } = string.Empty;
    public string ProtectedFolderName { get; set; } = "Radarr";
    public List<RadarrMovieDto> DownloadedMovies { get; set; } = new();
}

public sealed record BatchDeleteFilesRequest
{
    public List<int> MovieIds { get; init; } = new();
    public List<string>? FilePaths { get; init; }
}

public class RadarrReleaseDto
{
    public string Guid { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Indexer { get; set; } = string.Empty;
    public int IndexerId { get; set; }
    public int? Seeders { get; set; }
    public int? Leechers { get; set; }
    public string Protocol { get; set; } = "torrent";
    public string Quality { get; set; } = string.Empty;
    public double AgeHours { get; set; }
    public bool Approved { get; set; }
    public bool DownloadAllowed { get; set; }
    public List<string> Rejections { get; set; } = new();
    public DateTime? PublishDate { get; set; }
}

public class RadarrQueueItemDto
{
    public int Id { get; set; }
    public int MovieId { get; set; }
    public string? MovieTitle { get; set; }
    public string? ReleaseTitle { get; set; }
    public string Status { get; set; } = string.Empty;
    public double Size { get; set; }
    public double SizeLeft { get; set; }
    public double ProgressPercentage { get; set; }
    public string? TimeLeft { get; set; }
    public DateTime? EstimatedCompletionTime { get; set; }
    public string? Protocol { get; set; }
    public string? DownloadClient { get; set; }
    public string? ErrorMessage { get; set; }
    public double DownloadSpeed { get; set; }
    public double UploadSpeed { get; set; }
    public int ConnectedPeers { get; set; }
    public int? TransmissionId { get; set; }
    public string? FilePath { get; set; }
    public int FilesCount { get; set; }
    public bool IsDirectory { get; set; }
    public double MetadataPercentComplete { get; set; } = 1.0;
    public List<RadarrFileItemDto> Files { get; set; } = new();
}

public class RadarrAddMovieRequest
{
    public int TmdbId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Year { get; set; }
    public int? QualityProfileId { get; set; }
    public string? RootFolderPath { get; set; }
    public bool SearchForMovie { get; set; } = true;
    public string? PosterUrl { get; set; }
}

public class RadarrDownloadReleaseRequest
{
    public string Guid { get; set; } = string.Empty;
    public int IndexerId { get; set; }
}

public class RadarrQualityProfileDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class RadarrRootFolderDto
{
    public int Id { get; set; }
    public string Path { get; set; } = string.Empty;
    public long FreeSpace { get; set; }
}

public class RadarrTestConnectionRequest
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}

public class RadarrAutoGrabResultDto
{
    public bool Success { get; set; }
    public bool Grabbed { get; set; }
    public string? ReleaseTitle { get; set; }
    public string? Quality { get; set; }
    public string? Indexer { get; set; }
    public int? Seeders { get; set; }
    public long? Size { get; set; }
    public string? Source { get; set; }
    public string Message { get; set; } = string.Empty;
    public RadarrMovieDto? Movie { get; set; }
}

