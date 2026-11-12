using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application.AI;
using MyTube.Backend.Application.AI.Models;

using Microsoft.Extensions.DependencyInjection;

namespace MyTube.Backend.Infrastructure.Ai;

public class LiveSubtitleService : ILiveSubtitleService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LiveSubtitleService> _logger;

    private static readonly ConcurrentDictionary<string, LiveSubtitleSession> Sessions = new();

    public LiveSubtitleService(
        IServiceScopeFactory scopeFactory,
        ILogger<LiveSubtitleService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task<LiveSubtitleSession> StartSessionAsync(Guid videoId, SubtitleLanguage targetLanguage, string connectionId)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var session = new LiveSubtitleSession
        {
            SessionId = sessionId,
            VideoId = videoId,
            ConnectionId = connectionId,
            TargetLanguage = targetLanguage,
            CreatedAt = DateTime.UtcNow,
            LastActiveAt = DateTime.UtcNow
        };

        Sessions[sessionId] = session;
        _logger.LogInformation("Started Live Subtitle session {SessionId} for video {VideoId} in target language {TargetLanguage}.", sessionId, videoId, targetLanguage);
        return Task.FromResult(session);
    }

    public async Task<LiveSubtitleResultPacket?> ProcessAudioChunkAsync(
        string sessionId, 
        byte[] audioBytes, 
        string mimeType, 
        double timestampSeconds, 
        long sequenceNumber, 
        CancellationToken cancellationToken = default)
    {
        if (!Sessions.TryGetValue(sessionId, out var session))
        {
            _logger.LogWarning("Live Subtitle session {SessionId} not found or expired.", sessionId);
            return null;
        }

        session.LastActiveAt = DateTime.UtcNow;

        // Discard out-of-order chunks older than explicit seek baseline
        if (sequenceNumber < session.MinSequenceBaseline)
        {
            _logger.LogInformation("Discarding obsolete sequence number {SequenceNumber} for session {SessionId} (Min baseline: {MinSequenceBaseline}).", sequenceNumber, sessionId, session.MinSequenceBaseline);
            return null;
        }

        session.LastProcessedSequenceNumber = Math.Max(session.LastProcessedSequenceNumber, sequenceNumber);

        using var scope = _scopeFactory.CreateScope();
        var transcriptionRouter = scope.ServiceProvider.GetRequiredService<ITranscriptionRouter>();
        var translationRouter = scope.ServiceProvider.GetRequiredService<ITranslationRouter>();

        // 1. STT Transcription
        using var stream = new MemoryStream(audioBytes);
        var sttResult = await transcriptionRouter.TranscribeAudioStreamAsync(stream, mimeType, isLiveWorkload: true, cancellationToken);
        if (sttResult == null || string.IsNullOrWhiteSpace(sttResult.Text))
        {
            return null;
        }

        // Cache original transcript by sequence number (enables instant language switching without re-transcribing STT)
        session.TranscriptHistory[sequenceNumber] = sttResult.Text;

        var sourceLang = SubtitleLanguageExtensions.ParseLanguage(sttResult.DetectedLanguage);
        session.DetectedSourceLanguage = sourceLang;

        // 2. Translation Pass
        string finalSubtitleText = sttResult.Text;
        string translationProvider = "None";
        bool wasFallback = sttResult.ProviderName != "Groq";
        long totalLatencyMs = sttResult.LatencyMs;

        if (!SubtitleLanguageExtensions.IsSameLanguage(sourceLang, session.TargetLanguage) && session.TargetLanguage != SubtitleLanguage.Original)
        {
            var xlResult = await translationRouter.TranslateTextAsync(sttResult.Text, sourceLang, session.TargetLanguage, isLiveWorkload: true, cancellationToken);
            if (xlResult != null && !string.IsNullOrWhiteSpace(xlResult.TranslatedText))
            {
                finalSubtitleText = xlResult.TranslatedText;
                translationProvider = xlResult.ProviderName;
                if (xlResult.ProviderName != "Groq") wasFallback = true;
                totalLatencyMs += xlResult.LatencyMs;
            }
        }

        // Cache translation result
        session.TranslationHistory[$"{sequenceNumber}_{session.TargetLanguage}"] = finalSubtitleText;

        return new LiveSubtitleResultPacket
        {
            SessionId = sessionId,
            VideoId = session.VideoId,
            SequenceNumber = sequenceNumber,
            TimestampSeconds = timestampSeconds,
            Text = finalSubtitleText,
            OriginalText = sttResult.Text,
            Language = session.TargetLanguage.ToLanguageCode(),
            SttProviderUsed = sttResult.ProviderName,
            TranslationProviderUsed = translationProvider,
            WasFallbackUsed = wasFallback,
            LatencyMs = totalLatencyMs
        };
    }

    public async Task ChangeLanguageAsync(string sessionId, SubtitleLanguage targetLanguage, CancellationToken cancellationToken = default)
    {
        if (Sessions.TryGetValue(sessionId, out var session))
        {
            _logger.LogInformation("Updating target language for session {SessionId} from {OldLang} to {NewLang}.", sessionId, session.TargetLanguage, targetLanguage);
            session.TargetLanguage = targetLanguage;
            session.LastActiveAt = DateTime.UtcNow;
            await Task.CompletedTask;
        }
    }

    public Task SeekAsync(string sessionId, double timestampSeconds)
    {
        if (Sessions.TryGetValue(sessionId, out var session))
        {
            _logger.LogInformation("Seek triggered for session {SessionId} at position {TimestampSeconds}s. Updating sequence baseline.", sessionId, timestampSeconds);
            session.MinSequenceBaseline = session.LastProcessedSequenceNumber + 1;
            session.LastActiveAt = DateTime.UtcNow;
        }
        return Task.CompletedTask;
    }

    public Task StopSessionAsync(string sessionId)
    {
        if (Sessions.TryRemove(sessionId, out var session))
        {
            _logger.LogInformation("Stopped Live Subtitle session {SessionId}.", sessionId);
        }
        return Task.CompletedTask;
    }
}
