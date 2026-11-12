using System.Collections.Generic;

namespace MyTube.Backend.Application;

/// <summary>
/// Generic paginated response wrapper.
/// </summary>
public sealed record PagedResult<T>
{
    public List<T> Items { get; init; } = new List<T>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
