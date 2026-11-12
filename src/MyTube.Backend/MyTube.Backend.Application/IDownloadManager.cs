using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyTube.Backend.Application;

public interface IDownloadManager
{
    Task<DownloadJobDto> StartDownloadAsync(
        string url, 
        string? categoryName, 
        string? quality);
    Task<DownloadJobDto> StartDownloadAsync(DownloadRequest request);
    List<DownloadJobDto> GetJobs();
    bool CancelJob(Guid jobId);
    Task UpdateYtDlpAsync();
    Task<List<string>> GetAvailableQualitiesAsync(string url);
    Task<VideoProbeResultDto> ProbeVideoAsync(string url);
    Task<BatchDownloadResponseDto> StartBatchDownloadAsync(BatchDownloadRequest request, System.Threading.CancellationToken cancellationToken = default);
    Task<List<DownloadBatchDto>> GetBatchesAsync(System.Threading.CancellationToken cancellationToken = default);
    Task<DownloadBatchDto?> GetBatchByIdAsync(Guid batchId, System.Threading.CancellationToken cancellationToken = default);
    Task<PlaylistProbeResultDto> ProbePlaylistAsync(string url, System.Threading.CancellationToken cancellationToken = default);
    Task<bool> CancelBatchAsync(Guid batchId, System.Threading.CancellationToken cancellationToken = default);
    Task<int> CancelAllAsync(System.Threading.CancellationToken cancellationToken = default);
    Task<int> ClearFinishedJobsAsync(System.Threading.CancellationToken cancellationToken = default);
    Task<bool> DeleteBatchAsync(Guid batchId, System.Threading.CancellationToken cancellationToken = default);
    bool IsJobCancelled(Guid jobId);
}
