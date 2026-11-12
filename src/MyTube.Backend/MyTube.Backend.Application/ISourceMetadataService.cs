using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyTube.Backend.Application;

public interface ISourceMetadataService
{
    Task<VideoProbeResultDto> ProbeRemoteUrlAsync(string url, CancellationToken token = default);
    Task<List<string>> GetAvailableQualitiesAsync(string url, CancellationToken token = default);
    Task<PlaylistProbeResultDto> ProbePlaylistAsync(string url, CancellationToken token = default);
}
