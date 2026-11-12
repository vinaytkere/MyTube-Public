using System;
using System.Collections.Generic;

namespace MyTube.Backend.Domain;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsUserCreated { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ICollection<Video> Videos { get; set; } = new List<Video>();
}
