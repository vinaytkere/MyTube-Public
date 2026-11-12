using System;
using System.Threading;
using System.Threading.Tasks;
using MyTube.Backend.Domain;

namespace MyTube.Backend.Application;

public record DownloadJobProgressNotification(
    Guid JobId,
    Guid BatchId,
    string Title,
    DownloadStatus Status,
    string Phase,
    double Progress,
    string Speed,
    string Eta);

public record DownloadJobCompletedNotification(
    Guid JobId,
    Guid BatchId,
    string Title,
    string? FilePath);

public record DownloadJobFailedNotification(
    Guid JobId,
    Guid BatchId,
    string Title,
    string ErrorMessage);

public record DownloadBatchUpdatedNotification(
    Guid BatchId,
    DownloadStatus Status,
    int CompletedItems,
    int FailedItems,
    int TotalItems);

public interface IDownloadNotifier
{
    Task NotifyJobProgressAsync(DownloadJobProgressNotification notification, CancellationToken cancellationToken = default);
    Task NotifyJobCompletedAsync(DownloadJobCompletedNotification notification, CancellationToken cancellationToken = default);
    Task NotifyJobFailedAsync(DownloadJobFailedNotification notification, CancellationToken cancellationToken = default);
    Task NotifyBatchUpdatedAsync(DownloadBatchUpdatedNotification notification, CancellationToken cancellationToken = default);
}
