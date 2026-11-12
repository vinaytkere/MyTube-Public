using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application.AI;
using MyTube.Backend.Application.AI.Models;

namespace MyTube.Backend.Infrastructure.Ai.Translation;

public class GeminiTranslationProvider : ITranslationProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiTranslationProvider> _logger;

    public string ProviderName => "Gemini";

    public GeminiTranslationProvider(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GeminiTranslationProvider> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    private string? GetApiKey()
    {
        var key = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(key)) key = Environment.GetEnvironmentVariable("Gemini__ApiKey");
        if (string.IsNullOrWhiteSpace(key)) key = _configuration["GEMINI_API_KEY"];
        if (string.IsNullOrWhiteSpace(key)) key = _configuration["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(key)) key = _configuration["Gemini__ApiKey"];
        return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
    }

    public async Task<TranslationProviderResult?> TranslateTextAsync(
        string text, 
        SubtitleLanguage sourceLang, 
        SubtitleLanguage targetLang, 
        CancellationToken cancellationToken = default)
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Gemini API key not configured for translation.");
            return null;
        }

        var sw = Stopwatch.StartNew();
        var modelName = _configuration["AiOptions:LiveTranslation:GeminiModel"] ?? "gemini-2.0-flash";

        string targetLangName = targetLang switch
        {
            SubtitleLanguage.Kannada => "Kannada (ಕನ್ನಡ)",
            SubtitleLanguage.Hindi => "Hindi (हिंदी)",
            SubtitleLanguage.Spanish => "Spanish (Español)",
            _ => "English"
        };

        var prompt = $"Translate the following text accurately into {targetLangName}. Output ONLY the direct translated text:\n\"{text}\"";

        try
        {
            var requestUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";

            var requestBody = new
            {
                contents = new object[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new { temperature = 0.1 }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = jsonContent
            };
            request.Headers.UserAgent.ParseAdd("MyTube/1.0 (Windows NT 10.0; Win64; x64)");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini translation API returned {StatusCode}: {ErrorBody}", response.StatusCode, responseJson);
                throw new HttpRequestException(responseJson, null, response.StatusCode);
            }

            using var doc = JsonDocument.Parse(responseJson);
            var candidates = doc.RootElement.GetProperty("candidates");
            if (candidates.GetArrayLength() > 0)
            {
                var parts = candidates[0].GetProperty("content").GetProperty("parts");
                if (parts.GetArrayLength() > 0)
                {
                    var translated = parts[0].GetProperty("text").GetString();
                    if (!string.IsNullOrWhiteSpace(translated))
                    {
                        return new TranslationProviderResult
                        {
                            TranslatedText = translated.Trim().Trim('"'),
                            TargetLanguage = targetLang,
                            ProviderName = ProviderName,
                            ModelName = modelName,
                            LatencyMs = sw.ElapsedMilliseconds
                        };
                    }
                }
            }
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to translate text with Gemini.");
        }

        return null;
    }
}
