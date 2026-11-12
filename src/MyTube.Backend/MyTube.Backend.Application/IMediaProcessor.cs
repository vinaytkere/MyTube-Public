using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyTube.Backend.Application;

public interface IMediaProcessor
{
    string ResolvePhysicalFilePath(string filePath);
    Task<VideoMetadata?> ExtractMetadataAsync(string filePath);
    Task<bool> GenerateThumbnailAsync(string filePath, string destinationThumbnailPath, double durationSeconds);
    Task<List<string>> ExtractAudioChunksAsync(string filePath, string tempDir);

    Task<MediaProbeResult> ProbeAsync(string path, CancellationToken token = default) =>
        Task.FromResult(new MediaProbeResult { IsReadable = true });

    Task<MediaProcessResult> ProcessMediaAsync(MediaProcessRequest request, CancellationToken token = default) =>
        Task.FromResult(new MediaProcessResult { Success = true });

    Task<MediaValidationResult> ValidateOutputAsync(
        string path, 
        OutputMediaType expectedMediaType, 
        OutputFormat expectedFormat, 
        TimeSpan expectedDuration, 
        CancellationToken token = default) =>
        Task.FromResult(new MediaValidationResult { IsValid = true });
}
