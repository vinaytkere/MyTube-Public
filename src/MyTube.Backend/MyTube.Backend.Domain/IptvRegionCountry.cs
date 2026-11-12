namespace MyTube.Backend.Domain;

public class IptvRegionCountry
{
    public string RegionCode { get; set; } = string.Empty;
    public IptvRegion? Region { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public IptvCountry? Country { get; set; }
}

