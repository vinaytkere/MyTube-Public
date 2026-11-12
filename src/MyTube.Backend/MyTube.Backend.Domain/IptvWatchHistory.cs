using System;

namespace MyTube.Backend.Domain;

public class IptvWatchHistory
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public IptvChannel? Channel { get; set; }
    public int? LastStreamId { get; set; }
    public IptvStream? LastStream { get; set; }
    public DateTime WatchedAt { get; set; } = DateTime.UtcNow;
}

