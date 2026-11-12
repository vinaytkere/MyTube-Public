using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application;
using MyTube.Backend.Application.AI;

namespace MyTube.Backend.Infrastructure.Ai.Transcription;

public class AiTranscriptionRouter : ITranscriptionRouter
{
    private readonly IEnumerable<ITranscriptionProvider> _providers;
    private readonly IAiQuotaManager _quotaManager;
    private readonly ILogger<AiTranscriptionRouter> _logger;

    // Circuit breaker state per provider: providerName -> circuit opens until DateTime
    private static readonly ConcurrentDictionary<string, DateTime> CircuitBreakers = new();

    public AiTranscriptionRouter(
        IEnumerable<ITranscriptionProvider> providers,
        IAiQuotaManager quotaManager,
        ILogger<AiTranscriptionRouter> logger)
    {
        _providers = providers;
        _quotaManager = quotaManager;
        _logger = logger;
    }

    public async Task<TranscriptionProviderResult?> TranscribeAudioStreamAsync(
        Stream audioStream, 
        string mimeType, 
        bool isLiveWorkload = true, 
        CancellationToken cancellationToken = default)
    {
        var providersList = _providers.ToList();
        if (providersList.Count == 0)
        {
            _logger.LogError("No transcription providers registered in dependency injection.");
            return null;
        }

        var now = DateTime.UtcNow;

        foreach (var provider in providersList)
        {
            // Check Circuit Breaker
            if (CircuitBreakers.TryGetValue(provider.ProviderName, out var resumeTime))
            {
                if (now < resumeTime)
                {
                    _logger.LogWarning("Circuit breaker active for STT provider {ProviderName} until {ResumeTime}. Bypassing to next provider.", provider.ProviderName, resumeTime);
                    continue;
                }
                else
                {
                    CircuitBreakers.TryRemove(provider.ProviderName, out _);
                }
            }

            // Check Quota Manager for available model
            var quota = await _quotaManager.GetAvailableModelAsync("STT", provider.ProviderName);
            if (quota == null)
            {
                _logger.LogWarning("Quota limit reached or unavailable for STT provider {ProviderName}. Trying next provider.", provider.ProviderName);
                continue;
            }

            try
            {
                // Reset stream position if seekable
                if (audioStream.CanSeek) audioStream.Position = 0;

                var result = await provider.TranscribeAudioStreamAsync(audioStream, mimeType, cancellationToken);
                if (result != null && !string.IsNullOrWhiteSpace(result.Text))
                {
                    await _quotaManager.RecordRequestAsync(quota.Id);
                    return result;
                }
            }
            catch (HttpRequestException ex)
            {
                int statusCode = (int)(ex.StatusCode ?? HttpStatusCode.InternalServerError);
                await _quotaManager.RecordErrorAsync(quota.Id, statusCode, ex.Message);

                if (statusCode == 429)
                {
                    _logger.LogWarning("HTTP 429 Rate Limit encountered for STT provider {ProviderName}. Tripping circuit breaker for 30s.", provider.ProviderName);
                    CircuitBreakers[provider.ProviderName] = now.AddSeconds(30);
                }
                else if (statusCode >= 500)
                {
                    _logger.LogWarning("HTTP {StatusCode} Server Error for STT provider {ProviderName}. Tripping circuit breaker for 15s.", statusCode, provider.ProviderName);
                    CircuitBreakers[provider.ProviderName] = now.AddSeconds(15);
                }
                else
                {
                    _logger.LogWarning("HTTP {StatusCode} Request Error for STT provider {ProviderName}: {ErrorMessage}", statusCode, provider.ProviderName, ex.Message);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Transcription request canceled for provider {ProviderName}.", provider.ProviderName);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error executing transcription on provider {ProviderName}.", provider.ProviderName);
                await _quotaManager.RecordErrorAsync(quota.Id, 500, ex.Message);
            }
        }

        _logger.LogError("All STT transcription providers failed or exhausted quotas.");
        return null;
    }
}
