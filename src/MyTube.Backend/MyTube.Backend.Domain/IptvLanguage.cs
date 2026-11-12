using System.Collections.Generic;

namespace MyTube.Backend.Domain;

public class IptvLanguage
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public ICollection<IptvChannel> Channels { get; set; } = new List<IptvChannel>();
}

