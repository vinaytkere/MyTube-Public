import * as signalR from '@microsoft/signalr';
import { API_BASE_URL } from './api';

export interface LiveSubtitlePacket {
  sessionId: string;
  videoId: string;
  sequenceNumber: number;
  timestampSeconds: number;
  text: string;
  originalText: string;
  language: string;
  sttProviderUsed: string;
  translationProviderUsed: string;
  wasFallbackUsed: boolean;
  latencyMs: number;
}

function uint8ArrayToBase64(bytes: Uint8Array): string {
  let binary = '';
  const len = bytes.byteLength;
  for (let i = 0; i < len; i += 8192) {
    binary += String.fromCharCode.apply(null, Array.from(bytes.subarray(i, i + 8192)));
  }
  return window.btoa(binary);
}

class LiveSubtitleService {
  private connection: signalR.HubConnection | null = null;
  private subtitleCallback: ((packet: LiveSubtitlePacket) => void) | null = null;

  private getHubUrl(): string {
    const base = (import.meta.env.VITE_API_BASE_URL as string) || (import.meta.env.VITE_API_URL as string) || API_BASE_URL || 'http://localhost:5003';
    return `${base.replace(/\/$/, '')}/hubs/live-subtitles`;
  }

  public async initConnection(): Promise<signalR.HubConnection> {
    if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
      return this.connection;
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

    this.connection.on('ReceiveLiveSubtitle', (packet: LiveSubtitlePacket) => {
      if (this.subtitleCallback) {
        this.subtitleCallback(packet);
      }
    });

    await this.connection.start();
    return this.connection;
  }

  public onSubtitleReceived(callback: (packet: LiveSubtitlePacket) => void) {
    this.subtitleCallback = callback;
  }

  public async startSession(videoId: string, targetLanguage: string): Promise<string> {
    const conn = await this.initConnection();
    return await conn.invoke<string>('StartSession', videoId, targetLanguage);
  }

  public async sendAudioChunk(
    sessionId: string, 
    audioBytes: Uint8Array, 
    mimeType: string, 
    timestampSeconds: number, 
    sequenceNumber: number
  ): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      return;
    }

    const base64Str = uint8ArrayToBase64(audioBytes);
    await this.connection.invoke('SendAudioChunk', sessionId, base64Str, mimeType, timestampSeconds, sequenceNumber);
  }

  public async changeLanguage(sessionId: string, targetLanguage: string): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) return;
    await this.connection.invoke('ChangeLanguage', sessionId, targetLanguage);
  }

  public async seek(sessionId: string, timestampSeconds: number): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) return;
    await this.connection.invoke('Seek', sessionId, timestampSeconds);
  }

  public async stopSession(sessionId: string): Promise<void> {
    if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
      try {
        await this.connection.invoke('StopSession', sessionId);
      } catch {
        // Ignore disconnect race conditions
      }
      await this.connection.stop();
    }
    this.connection = null;
    this.subtitleCallback = null;
  }
}

export const liveSubtitleService = new LiveSubtitleService();
