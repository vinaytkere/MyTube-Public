using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MyTube.Backend.Application.Iptv;

public record StreamRelayResult(
    bool IsSuccess,
    int StatusCode,
    string ContentType,
    string? ManifestContent = null,
    Stream? StreamBody = null,
    string? ErrorMessage = null) : IDisposable
{
    public bool IsManifest => ManifestContent != null;

    public void Dispose()
    {
        StreamBody?.Dispose();
    }
}

public interface IIptvStreamRelayService
{
    Task<StreamRelayResult> RelayStreamAsync(
        string targetUrl,
        string? userAgent = null,
        string? referrer = null,
        CancellationToken ct = default);
}

