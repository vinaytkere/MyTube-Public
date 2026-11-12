import * as signalR from '@microsoft/signalr';
import { DownloadJobProgressNotification, DownloadBatchUpdatedNotification } from '../types';

type JobProgressCallback = (notification: DownloadJobProgressNotification) => void;
type JobCompletedCallback = (data: { jobId: string; batchId: string; title: string; filePath?: string }) => void;
type JobFailedCallback = (data: { jobId: string; batchId: string; title: string; errorMessage: string }) => void;
type BatchUpdatedCallback = (notification: DownloadBatchUpdatedNotification) => void;

class DownloadHubService {
  private connection: signalR.HubConnection | null = null;
  private progressListeners: Set<JobProgressCallback> = new Set();
  private completedListeners: Set<JobCompletedCallback> = new Set();
  private failedListeners: Set<JobFailedCallback> = new Set();
  private batchListeners: Set<BatchUpdatedCallback> = new Set();
  private connectingPromise: Promise<signalR.HubConnection> | null = null;

  private getHubUrl(): string {
    const base = (import.meta.env.VITE_API_BASE_URL as string) || (import.meta.env.VITE_API_URL as string) || 'http://localhost:5003';
    return `${base.replace(/\/$/, '')}/hubs/downloads`;
  }

  public async initConnection(): Promise<signalR.HubConnection> {
    if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
      return this.connection;
    }

    if (this.connectingPromise) {
      return this.connectingPromise;
    }

    const hubUrl = this.getHubUrl();
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        skipNegotiation: false,
        transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling
      })
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.connection.on('JobProgress', (notification: DownloadJobProgressNotification) => {
      this.progressListeners.forEach(cb => cb(notification));
    });

    this.connection.on('JobCompleted', (data: { jobId: string; batchId: string; title: string; filePath?: string }) => {
      this.completedListeners.forEach(cb => cb(data));
    });

    this.connection.on('JobFailed', (data: { jobId: string; batchId: string; title: string; errorMessage: string }) => {
      this.failedListeners.forEach(cb => cb(data));
    });

    this.connection.on('BatchUpdated', (notification: DownloadBatchUpdatedNotification) => {
      this.batchListeners.forEach(cb => cb(notification));
    });

    this.connectingPromise = (async () => {
      try {
        await this.connection!.start();
        return this.connection!;
      } catch (err) {
        console.warn('Failed to start SignalR connection for downloads hub; polling fallback will be used.', err);
        throw err;
      } finally {
        this.connectingPromise = null;
      }
    })();

    return this.connectingPromise;
  }

  public onJobProgress(callback: JobProgressCallback): () => void {
    this.progressListeners.add(callback);
    return () => this.progressListeners.delete(callback);
  }

  public onJobCompleted(callback: JobCompletedCallback): () => void {
    this.completedListeners.add(callback);
    return () => this.completedListeners.delete(callback);
  }

  public onJobFailed(callback: JobFailedCallback): () => void {
    this.failedListeners.add(callback);
    return () => this.failedListeners.delete(callback);
  }

  public onBatchUpdated(callback: BatchUpdatedCallback): () => void {
    this.batchListeners.add(callback);
    return () => this.batchListeners.delete(callback);
  }

  public async joinBatch(batchId: string): Promise<void> {
    if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
      await this.connection.invoke('JoinBatch', batchId);
    }
  }

  public async leaveBatch(batchId: string): Promise<void> {
    if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
      await this.connection.invoke('LeaveBatch', batchId);
    }
  }

  public disconnect(): void {
    if (this.connection) {
      this.connection.stop().catch(() => {});
      this.connection = null;
    }
  }
}

export const downloadHubService = new DownloadHubService();
