using System;
using System.Collections.Concurrent;

namespace MyTube.Backend.Application.AI.Models;

public class LiveSubtitleSession
{
    public string SessionId { get; set; } = string.Empty;
    public Guid VideoId { get; set; }
    public string ConnectionId { get; set; } = string.Empty;
    public SubtitleLanguage TargetLanguage { get; set; } = SubtitleLanguage.Original;
    public SubtitleLanguage DetectedSourceLanguage { get; set; } = SubtitleLanguage.English;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;
    public long LastProcessedSequenceNumber { get; set; } = 0;
    public long MinSequenceBaseline { get; set; } = 0;
    
    // Cache original transcripts by sequence number to avoid re-transcribing STT when switching target languages
    public ConcurrentDictionary<long, string> TranscriptHistory { get; } = new ConcurrentDictionary<long, string>();

    // Cache translated subtitles by key: $"{sequenceNumber}_{targetLanguage}"
    public ConcurrentDictionary<string, string> TranslationHistory { get; } = new ConcurrentDictionary<string, string>();
}

public class LiveSubtitleResultPacket
{
    public string SessionId { get; set; } = string.Empty;
    public Guid VideoId { get; set; }
    public long SequenceNumber { get; set; }
    public double TimestampSeconds { get; set; }
    public string Text { get; set; } = string.Empty;
    public string OriginalText { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public string SttProviderUsed { get; set; } = string.Empty;
    public string TranslationProviderUsed { get; set; } = string.Empty;
    public bool WasFallbackUsed { get; set; }
    public long LatencyMs { get; set; }
}
