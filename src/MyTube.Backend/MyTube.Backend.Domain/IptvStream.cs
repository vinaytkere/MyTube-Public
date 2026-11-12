using System;

namespace MyTube.Backend.Domain;

public class IptvStream
{
    public int Id { get; set; }
    public string ChannelId { get; set; } = string.Empty;
    public IptvChannel? Channel { get; set; }
    public string? Feed { get; set; }
    public string? Title { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Quality { get; set; }
    public string? UserAgent { get; set; }
    public string? Referrer { get; set; }
    public string? Labels { get; set; }
    public string Status { get; set; } = "Unknown";
    public DateTime? LastCheckedAt { get; set; }
    public int OrderIndex { get; set; }
    public bool IsActive { get; set; } = true;
}

