using System.Collections.Generic;

namespace MyTube.Backend.Domain;

public class IptvCountry
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Flag { get; set; }
    public string? LanguagesJson { get; set; }

    public ICollection<IptvChannel> Channels { get; set; } = new List<IptvChannel>();
    public ICollection<IptvRegionCountry> RegionCountries { get; set; } = new List<IptvRegionCountry>();
}

