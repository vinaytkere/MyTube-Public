using System;

namespace MyTube.Backend.Domain;

public class Favorite
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid VideoId { get; set; }
    public Video? Video { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
