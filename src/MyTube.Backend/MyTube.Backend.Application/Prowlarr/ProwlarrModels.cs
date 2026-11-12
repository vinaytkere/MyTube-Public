using System;
using System.Collections.Generic;

namespace MyTube.Backend.Application.Prowlarr;

public class ProwlarrSearchResultDto
{
    public string Guid { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? DownloadUrl { get; set; }
    public string? MagnetUrl { get; set; }
    public string? InfoUrl { get; set; }
    public long Size { get; set; }
    public string Indexer { get; set; } = string.Empty;
    public int IndexerId { get; set; }
    public int? Seeders { get; set; }
    public int? Leechers { get; set; }
    public string Protocol { get; set; } = "torrent";
    public DateTime? PublishDate { get; set; }
    public List<string> Categories { get; set; } = new();
    public List<int> CategoryIds { get; set; } = new();
    public string PrimaryCategory { get; set; } = "Other";
    public string CategoryBadge { get; set; } = "Files";
}

public class ProwlarrDownloadRequest
{
    public string Guid { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? DownloadUrl { get; set; }
    public string? MagnetUrl { get; set; }
}

public class TransmissionFileDto
{
    public string Name { get; set; } = string.Empty;
    public long Length { get; set; }
    public long BytesCompleted { get; set; }
}

public class TransmissionTorrentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double PercentDone { get; set; }
    public double RateDownload { get; set; }
    public double RateUpload { get; set; }
    public int Status { get; set; }
    public double SizeWhenDone { get; set; }
    public double LeftUntilDone { get; set; }
    public long Eta { get; set; }
    public int PeersSendingToUs { get; set; }
    public string? ErrorString { get; set; }
    public string? DownloadDir { get; set; }
    public string HashString { get; set; } = string.Empty;
    public double MetadataPercentComplete { get; set; } = 1.0;
    public int TrackersCount { get; set; }
    public int FilesCount { get; set; }
    public bool IsDirectory { get; set; }
    public List<TransmissionFileDto> Files { get; set; } = new();
}
