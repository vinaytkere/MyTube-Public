using System.Collections.Generic;

namespace MyTube.Backend.Domain;

public class IptvRegion
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public ICollection<IptvRegionCountry> RegionCountries { get; set; } = new List<IptvRegionCountry>();
}

