using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyTube.Backend.Application;

public interface IMediaProbeService
{
    Task<MediaProbeResult> ProbeFileAsync(string path, CancellationToken token = default);
    Task<VideoProbeResultDto> ProbeUrlAsync(string url, CancellationToken token = default);
    Task<PlaylistProbeResultDto> ProbePlaylistAsync(string url, CancellationToken token = default);
}
