using System;

namespace MyTube.Backend.Domain;

public class IptvFavorite
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public IptvChannel? Channel { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

