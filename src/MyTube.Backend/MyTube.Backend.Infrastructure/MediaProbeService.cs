using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MyTube.Backend.Application;
using MyTube.Backend.Infrastructure.Media;

namespace MyTube.Backend.Infrastructure;

/// <summary>
/// Composite implementation of IMediaProbeService delegating to FfprobeService and SourceMetadataService.
/// Eliminates code duplication across probing components while preserving full backward compatibility.
/// </summary>
public class MediaProbeService : IMediaProbeService
{
    private readonly IMediaProbeService _innerProbeService;

    public MediaProbeService(
        IMediaToolRunner toolRunner, 
        ILogger<MediaProbeService> logger,
        IDownloaderSettingsStore? settingsStore = null,
        IConfiguration? configuration = null)
    {
        var metaLogger = NullLogger<SourceMetadataService>.Instance;
        var ffLogger = NullLogger<FfprobeService>.Instance;
        var sourceMetaService = new SourceMetadataService(toolRunner, metaLogger, settingsStore, configuration);
        _innerProbeService = new FfprobeService(toolRunner, sourceMetaService, ffLogger);
    }

    public Task<MediaProbeResult> ProbeFileAsync(string path, CancellationToken token = default)
        => _innerProbeService.ProbeFileAsync(path, token);

    public Task<VideoProbeResultDto> ProbeUrlAsync(string url, CancellationToken token = default)
        => _innerProbeService.ProbeUrlAsync(url, token);

    public Task<PlaylistProbeResultDto> ProbePlaylistAsync(string url, CancellationToken token = default)
        => _innerProbeService.ProbePlaylistAsync(url, token);
}
