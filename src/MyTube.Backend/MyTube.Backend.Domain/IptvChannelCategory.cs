namespace MyTube.Backend.Domain;

public class IptvChannelCategory
{
    public string ChannelId { get; set; } = string.Empty;
    public IptvChannel? Channel { get; set; }
    public string CategoryId { get; set; } = string.Empty;
    public IptvCategory? Category { get; set; }
}

