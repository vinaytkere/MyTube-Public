using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application;
using MyTube.Backend.Application.AI;
using MyTube.Backend.Application.Security;
using MyTube.Backend.Domain;

namespace MyTube.Backend.Infrastructure.Ai;

/// <summary>
/// Single unified AI Service consolidating all AI operations across Cerebras, Groq, Gemini, DeepSeek, and OpenAI-compatible providers.
/// Replaces multiple separate classes with a single high-performance, polymorphic service adhering to DRY and SOLID.
/// </summary>
public class AiService : IAiService, IAiCompletionService, IAiAudioTranscriptionService
{
    private readonly HttpClient _httpClient;
    private readonly IAiQuotaManager? _quotaManager;
    private readonly IConfiguration _configuration;
    private readonly ISecretManagementService? _secretService;
    private readonly ILogger<AiService> _logger;

    public string ProviderName => "UnifiedAi";

    // Cerebras prioritized first for ultra-fast wafer-scale inference (>2000 tokens/sec), followed by Groq, Gemini, and DeepSeek
    private static readonly string[] DefaultFallbackOrder = ["Cerebras", "Groq", "Gemini", "DeepSeek"];

    public AiService(
        HttpClient httpClient,
        IAiQuotaManager? quotaManager,
        IConfiguration configuration,
        ISecretManagementService? secretService,
        ILogger<AiService> logger)
    {
        _httpClient = httpClient;
        _quotaManager = quotaManager;
        _configuration = configuration;
        _secretService = secretService;
        _logger = logger;
    }

    #region Text Completion & Analysis

    public async Task<string?> CompleteAsync(
        string providerName,
        string systemPrompt,
        string userPrompt,
        bool forceJsonMode = false,
        CancellationToken cancellationToken = default)
    {
        return providerName.ToLowerInvariant() switch
        {
            "cerebras" => await ExecuteOpenAiCompatibleAsync(
                "Cerebras",
                "https://api.cerebras.ai/v1/chat/completions",
                "Cerebras:ApiKey",
                "MyTube/1.0",
                systemPrompt,
                userPrompt,
                forceJsonMode,
                cancellationToken),

            "groq" => await ExecuteOpenAiCompatibleAsync(
                "Groq", 
                "https://api.groq.com/openai/v1/chat/completions", 
                "Groq:ApiKey", 
                "MyTube/1.0", 
                systemPrompt, 
                userPrompt, 
                forceJsonMode, 
                cancellationToken),

            "deepseek" => await ExecuteOpenAiCompatibleAsync(
                "DeepSeek", 
                "https://api.deepseek.com/chat/completions", 
                "DeepSeek:ApiKey", 
                null, 
                systemPrompt, 
                userPrompt, 
                forceJsonMode, 
                cancellationToken),

            "gemini" => await ExecuteGeminiAsync(
                systemPrompt, 
                userPrompt, 
                forceJsonMode, 
                cancellationToken),

            "ollama" => await ExecuteOpenAiCompatibleAsync(
                "Ollama",
                _configuration["Ollama:BaseUrl"] != null ? $"{_configuration["Ollama:BaseUrl"]!.TrimEnd('/')}/v1/chat/completions" : "http://localhost:11434/v1/chat/completions",
                "Ollama:ApiKey",
                null,
                systemPrompt,
                userPrompt,
                forceJsonMode,
                cancellationToken),

            _ => await ExecuteOpenAiCompatibleAsync(
                providerName,
                _configuration[$"Ai:Providers:{providerName}:Endpoint"] ?? "https://api.openai.com/v1/chat/completions",
                $"{providerName}:ApiKey",
                null,
                systemPrompt,
                userPrompt,
                forceJsonMode,
                cancellationToken)
        };
    }

    public async Task<string?> CompleteWithFallbackAsync(
        string systemPrompt,
        string userPrompt,
        bool forceJsonMode = false,
        CancellationToken cancellationToken = default)
    {
        foreach (var provider in DefaultFallbackOrder)
        {
            try
            {
                var apiKey = await ResolveApiKeyAsync($"{provider}:ApiKey");
                if (string.IsNullOrWhiteSpace(apiKey)) continue;

                _logger.LogInformation("Attempting AI completion via provider: {Provider}", provider);
                var result = await CompleteAsync(provider, systemPrompt, userPrompt, forceJsonMode, cancellationToken);
                if (!string.IsNullOrWhiteSpace(result))
                {
                    _logger.LogInformation("Successfully completed AI prompt via provider: {Provider}", provider);
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Provider {Provider} completion failed. Attempting next fallback...", provider);
            }
        }

        _logger.LogWarning("All AI providers in fallback chain exhausted or failed.");
        return null;
    }

    public async Task<AiAnalysisResult?> AnalyzeTextAsync(
        string originalFileName,
        string transcript,
        string systemCategoriesPrompt,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = AiPrompts.BuildMetadataSystemPrompt(systemCategoriesPrompt);
        var userPrompt = AiPrompts.BuildMetadataUserPrompt(originalFileName, transcript);

        var json = await CompleteWithFallbackAsync(systemPrompt, userPrompt, forceJsonMode: true, cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            return JsonSerializer.Deserialize<AiAnalysisResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize JSON analysis result from AI response.");
            return null;
        }
    }

    public async Task<string?> FormatAnswerAsync(
        string videoTitle,
        string question,
        string webContext,
        string videoSummary,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = AiPrompts.BuildQaSystemPrompt();
        var userPrompt = AiPrompts.BuildQaUserPrompt(videoTitle, videoSummary, "", question, webContext);
        return await CompleteWithFallbackAsync(systemPrompt, userPrompt, cancellationToken: cancellationToken);
    }

    // IAiCompletionService implementation
    Task<string?> IAiCompletionService.CompleteAsync(string systemPrompt, string userPrompt, bool forceJsonMode)
        => CompleteWithFallbackAsync(systemPrompt, userPrompt, forceJsonMode);

    #endregion

    #region Audio Transcription (STT)

    public async Task<string?> TranscribeAudioAsync(
        string audioFilePath, 
        string? providerName = null, 
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(audioFilePath))
        {
            _logger.LogWarning("Audio file not found: {Path}", audioFilePath);
            return null;
        }

        // Explicit provider request
        if (!string.IsNullOrWhiteSpace(providerName))
        {
            if (providerName.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
                return await TranscribeWithGeminiAsync(audioFilePath, cancellationToken);
            return await TranscribeWithGroqWhisperAsync(audioFilePath, cancellationToken);
        }

        // Automatic fallback: Groq Whisper -> Gemini
        var text = await TranscribeWithGroqWhisperAsync(audioFilePath, cancellationToken);
        if (!string.IsNullOrWhiteSpace(text)) return text;

        _logger.LogInformation("Groq Whisper transcription unavailable or failed. Falling back to Gemini STT for {Path}", audioFilePath);
        return await TranscribeWithGeminiAsync(audioFilePath, cancellationToken);
    }

    // IAiAudioTranscriptionService implementation
    Task<string?> IAiAudioTranscriptionService.TranscribeAudioAsync(string audioFilePath)
        => TranscribeAudioAsync(audioFilePath);

    private async Task<string?> TranscribeWithGroqWhisperAsync(string audioFilePath, CancellationToken cancellationToken)
    {
        var apiKey = await ResolveApiKeyAsync("Groq:ApiKey");
        if (string.IsNullOrWhiteSpace(apiKey)) return null;

        var quota = _quotaManager != null ? await _quotaManager.GetAvailableModelAsync("STT", "Groq") : null;
        var modelName = quota?.ModelName ?? "whisper-large-v3-turbo";

        for (int attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                await using var fileStream = File.OpenRead(audioFilePath);
                using var streamContent = new StreamContent(fileStream);

                streamContent.Headers.ContentType = new MediaTypeHeaderValue("audio/mpeg");
                content.Add(streamContent, "file", Path.GetFileName(audioFilePath));
                content.Add(new StringContent(modelName), "model");

                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/audio/transcriptions");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = content;

                var response = await _httpClient.SendAsync(request, cancellationToken);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("Groq Whisper rate limited (429) on attempt {Attempt}. Retrying...", attempt);
                    if (attempt < 2)
                    {
                        await Task.Delay(2000, cancellationToken);
                        continue;
                    }
                    if (quota != null && _quotaManager != null)
                    {
                        await _quotaManager.RecordErrorAsync(quota.Id, 429, "Too Many Requests (429)");
                    }
                    return null;
                }

                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Groq Whisper API call failed with status {StatusCode}: {ErrorBody}", response.StatusCode, responseBody);
                    if (quota != null && _quotaManager != null)
                    {
                        await _quotaManager.RecordErrorAsync(quota.Id, (int)response.StatusCode, responseBody);
                    }
                    return null;
                }

                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("text", out var textElement))
                {
                    var text = textElement.GetString();
                    if (quota != null && _quotaManager != null)
                    {
                        await _quotaManager.RecordRequestAsync(quota.Id);
                    }
                    return text?.Trim();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Groq Whisper transcription exception.");
                if (quota != null && _quotaManager != null)
                {
                    await _quotaManager.RecordErrorAsync(quota.Id, 500, ex.Message);
                }
            }
        }

        return null;
    }

    private async Task<string?> TranscribeWithGeminiAsync(string audioFilePath, CancellationToken cancellationToken)
    {
        var apiKey = await ResolveApiKeyAsync("Gemini:ApiKey");
        if (string.IsNullOrWhiteSpace(apiKey)) return null;

        var quota = _quotaManager != null ? await _quotaManager.GetAvailableModelAsync("Text", "Gemini") : null;
        var modelName = quota?.ModelName ?? "gemini-2.5-flash";

        try
        {
            byte[] audioBytes = await File.ReadAllBytesAsync(audioFilePath, cancellationToken);
            string base64Data = Convert.ToBase64String(audioBytes);

            var requestUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";

            var requestBody = new
            {
                contents = new object[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = "audio/mp3",
                                    data = base64Data
                                }
                            },
                            new
                            {
                                text = "Transcribe the spoken audio verbatim into clean text. Output ONLY the exact transcribed text without any intro, metadata, or extra explanation."
                            }
                        }
                    }
                },
                generationConfig = new { temperature = 0.2 }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = jsonContent
            };
            request.Headers.UserAgent.ParseAdd("MyTube/1.0 (Windows NT 10.0; Win64; x64)");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Gemini audio transcription failed with status {StatusCode}: {ErrorBody}", response.StatusCode, responseJson);
                if (quota != null && _quotaManager != null)
                {
                    await _quotaManager.RecordErrorAsync(quota.Id, (int)response.StatusCode, responseJson);
                }
                return null;
            }

            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                var parts = candidates[0].GetProperty("content").GetProperty("parts");
                if (parts.GetArrayLength() > 0)
                {
                    var text = parts[0].GetProperty("text").GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        if (quota != null && _quotaManager != null)
                        {
                            await _quotaManager.RecordRequestAsync(quota.Id);
                        }
                        return text.Trim();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini audio transcription failed.");
            if (quota != null && _quotaManager != null)
            {
                await _quotaManager.RecordErrorAsync(quota.Id, 500, ex.Message);
            }
        }

        return null;
    }

    #endregion

    #region Provider Engines & Helpers

    private async Task<string?> ExecuteOpenAiCompatibleAsync(
        string providerName,
        string endpointUrl,
        string apiKeySecretKey,
        string? customUserAgent,
        string systemPrompt,
        string userPrompt,
        bool forceJsonMode,
        CancellationToken cancellationToken)
    {
        var apiKey = await ResolveApiKeyAsync(apiKeySecretKey);
        if (string.IsNullOrWhiteSpace(apiKey)) return null;

        var quota = _quotaManager != null ? await _quotaManager.GetAvailableModelAsync("Text", providerName) : null;
        var modelName = quota?.ModelName ?? GetDefaultModel(providerName);

        try
        {
            var messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            };

            object requestBodyObj = forceJsonMode
                ? new
                {
                    model = modelName,
                    messages = messages,
                    response_format = new { type = "json_object" },
                    temperature = 0.3
                }
                : new
                {
                    model = modelName,
                    messages = messages,
                    temperature = 0.3
                };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBodyObj), Encoding.UTF8, "application/json");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            using var request = new HttpRequestMessage(HttpMethod.Post, endpointUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            if (!string.IsNullOrWhiteSpace(customUserAgent))
            {
                request.Headers.UserAgent.ParseAdd(customUserAgent);
            }
            request.Content = jsonContent;

            var response = await _httpClient.SendAsync(request, cts.Token);
            var responseJson = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("{Provider} API call failed with status {StatusCode}: {ErrorBody}", providerName, response.StatusCode, responseJson);
                if (quota != null && _quotaManager != null)
                {
                    await _quotaManager.RecordErrorAsync(quota.Id, (int)response.StatusCode, responseJson);
                }
                return null;
            }

            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var content = choices[0].GetProperty("message").GetProperty("content").GetString();
                if (!string.IsNullOrWhiteSpace(content))
                {
                    if (quota != null && _quotaManager != null)
                    {
                        await _quotaManager.RecordRequestAsync(quota.Id);
                    }
                    return content.Trim();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Provider} completion request failed.", providerName);
            if (quota != null && _quotaManager != null)
            {
                await _quotaManager.RecordErrorAsync(quota.Id, 500, ex.Message);
            }
        }

        return null;
    }

    private async Task<string?> ExecuteGeminiAsync(
        string systemPrompt,
        string userPrompt,
        bool forceJsonMode,
        CancellationToken cancellationToken)
    {
        var apiKey = await ResolveApiKeyAsync("Gemini:ApiKey");
        if (string.IsNullOrWhiteSpace(apiKey)) return null;

        var attemptedQuotaIds = new HashSet<int>();

        var firstQuota = _quotaManager != null ? await _quotaManager.GetAvailableModelAsync("Text", "Gemini") : null;
        if (firstQuota == null)
        {
            return await ExecuteSingleGeminiRequestAsync(GetDefaultModel("Gemini"), apiKey, systemPrompt, userPrompt, forceJsonMode, null, cancellationToken);
        }

        while (attemptedQuotaIds.Count < 5)
        {
            var quota = await _quotaManager!.GetAvailableModelAsync("Text", "Gemini");
            if (quota == null || attemptedQuotaIds.Contains(quota.Id))
            {
                _logger.LogWarning("No further available Gemini models in quota pool.");
                return null;
            }

            attemptedQuotaIds.Add(quota.Id);

            var result = await ExecuteSingleGeminiRequestAsync(quota.ModelName, apiKey, systemPrompt, userPrompt, forceJsonMode, quota, cancellationToken);
            if (!string.IsNullOrWhiteSpace(result))
            {
                return result;
            }
        }

        return null;
    }

    private async Task<string?> ExecuteSingleGeminiRequestAsync(
        string modelName,
        string apiKey,
        string systemPrompt,
        string userPrompt,
        bool forceJsonMode,
        AiModelQuota? quota,
        CancellationToken cancellationToken)
    {
        try
        {
            var requestUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";

            object generationConfig = forceJsonMode
                ? new { response_mime_type = "application/json", temperature = 0.3 }
                : new { temperature = 0.3 };

            var requestBody = new
            {
                contents = new object[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = $"{systemPrompt}\n\n{userPrompt}" }
                        }
                    }
                },
                generationConfig = generationConfig
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = jsonContent
            };
            request.Headers.UserAgent.ParseAdd("MyTube/1.0 (Windows NT 10.0; Win64; x64)");

            var response = await _httpClient.SendAsync(request, cts.Token);
            var responseJson = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Gemini API call failed with status {StatusCode}: {ErrorBody}", response.StatusCode, responseJson);
                if (quota != null && _quotaManager != null)
                {
                    await _quotaManager.RecordErrorAsync(quota.Id, (int)response.StatusCode, responseJson);
                }
                return null;
            }

            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                var parts = candidates[0].GetProperty("content").GetProperty("parts");
                if (parts.GetArrayLength() > 0)
                {
                    var text = parts[0].GetProperty("text").GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        if (quota != null && _quotaManager != null)
                        {
                            await _quotaManager.RecordRequestAsync(quota.Id);
                        }
                        _logger.LogInformation("Successfully processed request using Gemini model {Model}", modelName);
                        return text.Trim();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini model {Model} failed.", modelName);
            if (quota != null && _quotaManager != null)
            {
                await _quotaManager.RecordErrorAsync(quota.Id, 500, ex.Message);
            }
        }

        return null;
    }

    private static string GetDefaultModel(string provider) => provider.ToLowerInvariant() switch
    {
        "cerebras" => "llama3.1-70b",
        "groq" => "llama-3.3-70b-versatile",
        "deepseek" => "deepseek-chat",
        "gemini" => "gemini-2.5-flash",
        "ollama" => "phi",
        _ => "gpt-4o-mini"
    };

    private async Task<string?> ResolveApiKeyAsync(string secretKey)
    {
        if (_secretService != null)
        {
            var secret = await _secretService.GetSecretAsync(secretKey);
            if (!string.IsNullOrWhiteSpace(secret)) return secret;
        }

        var envKey = secretKey.Replace(":", "_").ToUpperInvariant();
        var key = Environment.GetEnvironmentVariable(envKey)
                  ?? Environment.GetEnvironmentVariable(secretKey)
                  ?? _configuration[secretKey]
                  ?? _configuration[envKey];

        return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
    }

    #endregion
}
