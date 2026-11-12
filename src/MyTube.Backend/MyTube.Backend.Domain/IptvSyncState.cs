using System;

namespace MyTube.Backend.Domain;

public class IptvSyncState
{
    public int Id { get; set; }
    public string DatasetName { get; set; } = string.Empty;
    public string? ETag { get; set; }
    public DateTime? LastModified { get; set; }
    public DateTime LastSyncAt { get; set; } = DateTime.UtcNow;
    public int ItemCount { get; set; }
    public string Status { get; set; } = "Success";
    public string? ErrorMessage { get; set; }
}

