using System;
using System.Collections.Generic;

namespace MyTube.Backend.Application.Sonarr;

public class SonarrStatusDto
{
    public bool IsConfigured { get; set; }
    public bool IsConnected { get; set; }
    public string? Version { get; set; }
    public string? AppName { get; set; }
    public string? ErrorMessage { get; set; }
}

public class SonarrSeasonDto
{
    public int SeasonNumber { get; set; }
    public bool Monitored { get; set; }
    public int TotalEpisodeCount { get; set; }
    public int EpisodeCount { get; set; }
    public int EpisodeFileCount { get; set; }
    public long SizeOnDisk { get; set; }
}

public class SonarrSeriesDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? CleanTitle { get; set; }
    public string? SortTitle { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Overview { get; set; }
    public string? Network { get; set; }
    public string? AirTime { get; set; }
    public List<string> Images { get; set; } = new();
    public string? PosterUrl { get; set; }
    public string? FanartUrl { get; set; }
    public int Year { get; set; }
    public string? Path { get; set; }
    public int QualityProfileId { get; set; }
    public bool SeasonFolder { get; set; }
    public bool Monitored { get; set; }
    public int TvdbId { get; set; }
    public int TvRageId { get; set; }
    public int TvMazeId { get; set; }
    public string? ImdbId { get; set; }
    public string? SeriesType { get; set; }
    public int SeasonCount { get; set; }
    public int TotalEpisodeCount { get; set; }
    public int EpisodeCount { get; set; }
    public int EpisodeFileCount { get; set; }
    public long SizeOnDisk { get; set; }
    public List<string> Genres { get; set; } = new();
    public double? RatingsValue { get; set; }
    public List<SonarrSeasonDto> Seasons { get; set; } = new();
    
    // Homelab Cross-reference
    public bool IsAvailableInHomelab { get; set; }
    public int HomelabMatchedEpisodes { get; set; }
}

public class SonarrEpisodeDto
{
    public int Id { get; set; }
    public int SeriesId { get; set; }
    public int EpisodeFileId { get; set; }
    public int SeasonNumber { get; set; }
    public int EpisodeNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? AirDate { get; set; }
    public DateTime? AirDateUtc { get; set; }
    public string? Overview { get; set; }
    public bool HasFile { get; set; }
    public bool Monitored { get; set; }
    public string? Quality { get; set; }
    public long? Size { get; set; }
    public Guid? LocalVideoId { get; set; }
}

public class SonarrAddSeriesRequest
{
    public int TvdbId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int QualityProfileId { get; set; } = 1;
    public string? RootFolderPath { get; set; }
    public bool Monitored { get; set; } = true;
    public bool SeasonFolder { get; set; } = true;
    public bool SearchForMissingEpisodes { get; set; } = true;
    public string SeriesType { get; set; } = "standard";
}

public class SonarrReleaseDto
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
    public List<string> Rejections { get; set; } = new();
    public DateTime? PublishDate { get; set; }
}

public class SonarrDownloadReleaseRequest
{
    public string Guid { get; set; } = string.Empty;
    public int IndexerId { get; set; }
    public int? SeriesId { get; set; }
    public int? EpisodeId { get; set; }
}

public class SonarrQueueItemDto
{
    public int Id { get; set; }
    public int SeriesId { get; set; }
    public int EpisodeId { get; set; }
    public string SeriesTitle { get; set; } = string.Empty;
    public string EpisodeTitle { get; set; } = string.Empty;
    public int SeasonNumber { get; set; }
    public int EpisodeNumber { get; set; }
    public string Quality { get; set; } = string.Empty;
    public long Size { get; set; }
    public double Sizeleft { get; set; }
    public string Status { get; set; } = string.Empty;
    public string TrackedDownloadStatus { get; set; } = string.Empty;
    public string? Timeleft { get; set; }
    public double Progress { get; set; }
}

public class SonarrQualityProfileDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class SonarrRootFolderDto
{
    public int Id { get; set; }
    public string Path { get; set; } = string.Empty;
    public long FreeSpace { get; set; }
}
