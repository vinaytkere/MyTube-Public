using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application;
using MyTube.Backend.Application.AI;
using MyTube.Backend.Application.AI.Models;

namespace MyTube.Backend.Infrastructure.Ai.Translation;

public class AiTranslationRouter : ITranslationRouter
{
    private readonly IEnumerable<ITranslationProvider> _providers;
    private readonly IAiQuotaManager _quotaManager;
    private readonly ILogger<AiTranslationRouter> _logger;

    private static readonly ConcurrentDictionary<string, DateTime> CircuitBreakers = new();

    public AiTranslationRouter(
        IEnumerable<ITranslationProvider> providers,
        IAiQuotaManager quotaManager,
        ILogger<AiTranslationRouter> logger)
    {
        _providers = providers;
        _quotaManager = quotaManager;
        _logger = logger;
    }

    public async Task<TranslationProviderResult?> TranslateTextAsync(
        string text, 
        SubtitleLanguage sourceLang, 
        SubtitleLanguage targetLang, 
        bool isLiveWorkload = true, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // Bypass translation if source and target languages match or target is Original
        if (SubtitleLanguageExtensions.IsSameLanguage(sourceLang, targetLang) || targetLang == SubtitleLanguage.Original)
        {
            return new TranslationProviderResult
            {
                TranslatedText = text,
                TargetLanguage = targetLang,
                ProviderName = "None (Bypassed)",
                ModelName = "Identity",
                LatencyMs = 0
            };
        }

        var providersList = _providers.ToList();
        if (providersList.Count == 0)
        {
            _logger.LogError("No translation providers registered.");
            return null;
        }

        var now = DateTime.UtcNow;

        foreach (var provider in providersList)
        {
            if (CircuitBreakers.TryGetValue(provider.ProviderName, out var resumeTime))
            {
                if (now < resumeTime)
                {
                    _logger.LogWarning("Circuit breaker active for Translation provider {ProviderName} until {ResumeTime}. Bypassing.", provider.ProviderName, resumeTime);
                    continue;
                }
                else
                {
                    CircuitBreakers.TryRemove(provider.ProviderName, out _);
                }
            }

            var quota = await _quotaManager.GetAvailableModelAsync("Text", provider.ProviderName);
            if (quota == null)
            {
                _logger.LogWarning("Quota exhausted for Translation provider {ProviderName}. Bypassing to next provider.", provider.ProviderName);
                continue;
            }

            try
            {
                var result = await provider.TranslateTextAsync(text, sourceLang, targetLang, cancellationToken);
                if (result != null && !string.IsNullOrWhiteSpace(result.TranslatedText))
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
                    _logger.LogWarning("HTTP 429 Rate Limit encountered for Translation provider {ProviderName}. Tripping circuit breaker for 30s.", provider.ProviderName);
                    CircuitBreakers[provider.ProviderName] = now.AddSeconds(30);
                }
                else if (statusCode >= 500)
                {
                    _logger.LogWarning("HTTP {StatusCode} Server Error for Translation provider {ProviderName}. Tripping circuit breaker for 15s.", statusCode, provider.ProviderName);
                    CircuitBreakers[provider.ProviderName] = now.AddSeconds(15);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error executing translation on provider {ProviderName}.", provider.ProviderName);
                await _quotaManager.RecordErrorAsync(quota.Id, 500, ex.Message);
            }
        }

        _logger.LogError("All translation providers failed or exhausted quotas.");
        return null;
    }
}
