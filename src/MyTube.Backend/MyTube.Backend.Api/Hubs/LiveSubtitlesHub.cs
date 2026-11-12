using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application.AI;
using MyTube.Backend.Application.AI.Models;

namespace MyTube.Backend.Api.Hubs;

public class LiveSubtitlesHub : Hub
{
    private readonly ILiveSubtitleService _liveSubtitleService;
    private readonly ILogger<LiveSubtitlesHub> _logger;

    public LiveSubtitlesHub(
        ILiveSubtitleService liveSubtitleService,
        ILogger<LiveSubtitlesHub> logger)
    {
        _liveSubtitleService = liveSubtitleService;
        _logger = logger;
    }

    public async Task<string> StartSession(Guid videoId, string targetLanguage)
    {
        var language = SubtitleLanguageExtensions.ParseLanguage(targetLanguage);
        var session = await _liveSubtitleService.StartSessionAsync(videoId, language, Context.ConnectionId);
        _logger.LogInformation("SignalR Client {ConnectionId} started session {SessionId} for video {VideoId}.", Context.ConnectionId, session.SessionId, videoId);
        return session.SessionId;
    }

    public async Task SendAudioChunk(string sessionId, string audioBase64, string mimeType, double timestampSeconds, long sequenceNumber)
    {
        if (string.IsNullOrWhiteSpace(audioBase64)) return;

        try
        {
            var audioBytes = Convert.FromBase64String(audioBase64);
            var packet = await _liveSubtitleService.ProcessAudioChunkAsync(sessionId, audioBytes, mimeType, timestampSeconds, sequenceNumber, Context.ConnectionAborted);
            if (packet != null)
            {
                await Clients.Caller.SendAsync("ReceiveLiveSubtitle", packet);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected or seek triggered
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing audio chunk for session {SessionId} on hub.", sessionId);
        }
    }

    public async Task ChangeLanguage(string sessionId, string targetLanguage)
    {
        var language = SubtitleLanguageExtensions.ParseLanguage(targetLanguage);
        await _liveSubtitleService.ChangeLanguageAsync(sessionId, language, Context.ConnectionAborted);
        await Clients.Caller.SendAsync("LanguageChanged", sessionId, language.ToLanguageCode());
    }

    public async Task Seek(string sessionId, double timestampSeconds)
    {
        await _liveSubtitleService.SeekAsync(sessionId, timestampSeconds);
        await Clients.Caller.SendAsync("SeekProcessed", sessionId, timestampSeconds);
    }

    public async Task StopSession(string sessionId)
    {
        await _liveSubtitleService.StopSessionAsync(sessionId);
        await Clients.Caller.SendAsync("SessionStopped", sessionId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("SignalR Client {ConnectionId} disconnected.", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
