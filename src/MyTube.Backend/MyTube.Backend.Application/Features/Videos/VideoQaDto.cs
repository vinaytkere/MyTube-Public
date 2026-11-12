using System;

namespace MyTube.Backend.Application.Features.Videos;

public sealed record VideoQaDto
{
    public int Id { get; init; }
    public Guid VideoId { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public string Answer { get; init; } = string.Empty;
    public string? FirecrawlContext { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
