using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MyTube.Backend.Application.Iptv;

// === Raw external iptv-org API models ===

public class IptvOrgChannelDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("alt_names")]
    public List<string>? AltNames { get; set; }

    [JsonPropertyName("network")]
    public string? Network { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("subdivision")]
    public string? Subdivision { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("categories")]
    public List<string>? Categories { get; set; }

    [JsonPropertyName("is_nsfw")]
    public bool IsNsfw { get; set; }

    [JsonPropertyName("launched")]
    public string? Launched { get; set; }

    [JsonPropertyName("closed")]
    public string? Closed { get; set; }

    [JsonPropertyName("replaced_by")]
    public string? ReplacedBy { get; set; }

    [JsonPropertyName("website")]
    public string? Website { get; set; }
}

public class IptvOrgStreamDto
{
    [JsonPropertyName("channel")]
    public string? Channel { get; set; }

    [JsonPropertyName("feed")]
    public string? Feed { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("quality")]
    public string? Quality { get; set; }

    [JsonPropertyName("user_agent")]
    public string? UserAgent { get; set; }

    [JsonPropertyName("referrer")]
    public string? Referrer { get; set; }

    [JsonPropertyName("labels")]
    public List<string>? Labels { get; set; }
}

public class IptvOrgCountryDto
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("flag")]
    public string? Flag { get; set; }

    [JsonPropertyName("languages")]
    public List<string>? Languages { get; set; }
}

public class IptvOrgRegionDto
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("countries")]
    public List<string>? Countries { get; set; }
}

public class IptvOrgCategoryDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public class IptvOrgLanguageDto
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class IptvOrgLogoDto
{
    [JsonPropertyName("channel")]
    public string Channel { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("format")]
    public string? Format { get; set; }

    [JsonPropertyName("in_use")]
    public bool InUse { get; set; }
}

public class IptvOrgFeedDto
{
    [JsonPropertyName("channel")]
    public string Channel { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("languages")]
    public List<string>? Languages { get; set; }

    [JsonPropertyName("is_main")]
    public bool IsMain { get; set; }

    [JsonPropertyName("format")]
    public string? Format { get; set; }
}

public class IptvOrgBlocklistDto
{
    [JsonPropertyName("channel")]
    public string Channel { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

// === Application Client DTOs ===

public class IptvStreamDto
{
    public int Id { get; set; }
    public string? Feed { get; set; }
    public string? Title { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Quality { get; set; }
    public string? UserAgent { get; set; }
    public string? Referrer { get; set; }
    public string? Labels { get; set; }
    public string Status { get; set; } = "Unknown";
    public int OrderIndex { get; set; }
}

public class IptvChannelDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? AltNames { get; set; }
    public string? Network { get; set; }
    public string? CountryCode { get; set; }
    public string? CountryName { get; set; }
    public string? CountryFlag { get; set; }
    public string? PrimaryLanguageCode { get; set; }
    public string? PrimaryLanguageName { get; set; }
    public string? Website { get; set; }
    public string? LogoUrl { get; set; }
    public bool IsFavorite { get; set; }
    public DateTime? LastWatchedAt { get; set; }
    public List<string> Categories { get; set; } = new();
    public List<IptvStreamDto> Streams { get; set; } = new();
}

public class IptvFilterOptionsDto
{
    public List<IptvRegionFilterDto> Regions { get; set; } = new();
    public List<IptvCountryFilterDto> Countries { get; set; } = new();
    public List<IptvCategoryFilterDto> Categories { get; set; } = new();
    public List<IptvLanguageFilterDto> Languages { get; set; } = new();
    public int TotalChannels { get; set; }
    public int TotalStreams { get; set; }
}

public class IptvRegionFilterDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int ChannelCount { get; set; }
}

public class IptvCountryFilterDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Flag { get; set; }
    public int ChannelCount { get; set; }
}

public class IptvCategoryFilterDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int ChannelCount { get; set; }
}

public class IptvLanguageFilterDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int ChannelCount { get; set; }
}

public class IptvSyncStatusDto
{
    public bool IsSyncing { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public int ChannelsCount { get; set; }
    public int StreamsCount { get; set; }
    public string Status { get; set; } = "Idle";
    public string? LastError { get; set; }
    public List<IptvSyncStateDto> States { get; set; } = new();
}

public class IptvSyncStateDto
{
    public string DatasetName { get; set; } = string.Empty;
    public DateTime LastSyncAt { get; set; }
    public int ItemCount { get; set; }
    public string Status { get; set; } = "Success";
}

