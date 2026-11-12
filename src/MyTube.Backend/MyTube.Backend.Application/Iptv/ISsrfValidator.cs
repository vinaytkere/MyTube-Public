using System.Threading;
using System.Threading.Tasks;

namespace MyTube.Backend.Application.Iptv;

public interface ISsrfValidator
{
    /// <summary>
    /// Validates an external URL for SSRF threats.
    /// Checks URI scheme, ensures hostname is not loopback, private subnet (RFC 1918),
    /// link-local, or cloud metadata endpoints.
    /// </summary>
    Task<SsrfValidationResult> ValidateUrlAsync(string url, CancellationToken ct = default);
}

public class SsrfValidationResult
{
    public bool IsSafe { get; set; }
    public string? Reason { get; set; }

    public static SsrfValidationResult Success() => new() { IsSafe = true };
    public static SsrfValidationResult Blocked(string reason) => new() { IsSafe = false, Reason = reason };
}

