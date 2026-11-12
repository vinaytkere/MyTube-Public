namespace MyTube.Backend.Application;

/// <summary>
/// Data transfer record for media categories.
/// </summary>
public sealed record CategoryDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int VideoCount { get; init; }
    public bool IsUserCreated { get; init; }
}
