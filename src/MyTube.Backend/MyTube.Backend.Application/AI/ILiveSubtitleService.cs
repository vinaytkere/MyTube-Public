using System;
using System.Threading;
using System.Threading.Tasks;
using MyTube.Backend.Application.AI.Models;

namespace MyTube.Backend.Application.AI;

public interface ILiveSubtitleService
{
    Task<LiveSubtitleSession> StartSessionAsync(Guid videoId, SubtitleLanguage targetLanguage, string connectionId);
    Task<LiveSubtitleResultPacket?> ProcessAudioChunkAsync(string sessionId, byte[] audioBytes, string mimeType, double timestampSeconds, long sequenceNumber, CancellationToken cancellationToken = default);
    Task ChangeLanguageAsync(string sessionId, SubtitleLanguage targetLanguage, CancellationToken cancellationToken = default);
    Task SeekAsync(string sessionId, double timestampSeconds);
    Task StopSessionAsync(string sessionId);
}
