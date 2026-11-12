using System;
using System.Collections.Generic;

namespace MyTube.Backend.Domain;

public class IptvChannel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? AltNames { get; set; }
    public string? Network { get; set; }
    public string? CountryCode { get; set; }
    public IptvCountry? Country { get; set; }
    public string? Subdivision { get; set; }
    public string? City { get; set; }
    public string? Website { get; set; }
    public string? LogoUrl { get; set; }
    public string? PrimaryLanguageCode { get; set; }
    public IptvLanguage? PrimaryLanguage { get; set; }
    public bool IsNsfw { get; set; }
    public bool IsClosed { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<IptvStream> Streams { get; set; } = new List<IptvStream>();
    public ICollection<IptvChannelCategory> ChannelCategories { get; set; } = new List<IptvChannelCategory>();
}

