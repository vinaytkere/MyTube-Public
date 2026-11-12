using System.Collections.Generic;

namespace MyTube.Backend.Domain;

public class IptvCategory
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<IptvChannelCategory> ChannelCategories { get; set; } = new List<IptvChannelCategory>();
}

