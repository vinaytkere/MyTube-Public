using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application.AI;
using MyTube.Backend.Application.AI.Models;

namespace MyTube.Backend.Infrastructure.Ai.Translation;

public class FastLlamaTranslationProvider : ITranslationProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FastLlamaTranslationProvider> _logger;

    public string ProviderName => "Groq";

    public FastLlamaTranslationProvider(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<FastLlamaTranslationProvider> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    private string? GetApiKey()
    {
        var key = Environment.GetEnvironmentVariable("GROQ_API_KEY");
        if (string.IsNullOrWhiteSpace(key)) key = Environment.GetEnvironmentVariable("Groq__ApiKey");
        if (string.IsNullOrWhiteSpace(key)) key = _configuration["GROQ_API_KEY"];
        if (string.IsNullOrWhiteSpace(key)) key = _configuration["Groq:ApiKey"];
        if (string.IsNullOrWhiteSpace(key)) key = _configuration["Groq__ApiKey"];
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
            _logger.LogWarning("Groq API key not configured for translation.");
            return null;
        }

        var sw = Stopwatch.StartNew();
        var modelName = _configuration["AiOptions:LiveTranslation:GroqModel"] ?? "llama-3.3-70b-versatile";

        string targetLangName = targetLang switch
        {
            SubtitleLanguage.Kannada => "Kannada (ಕನ್ನಡ)",
            SubtitleLanguage.Hindi => "Hindi (हिंदी)",
            SubtitleLanguage.Spanish => "Spanish (Español)",
            _ => "English"
        };

        var systemPrompt = "You are a professional real-time live subtitle translator. Translate the provided subtitle text accurately into " + targetLangName + ". Output ONLY the direct translated sentence without quotes, notes, or intros.";
        var userPrompt = $"Text to translate: \"{text}\"";

        try
        {
            var requestBody = new
            {
                model = modelName,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.2
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = jsonContent;

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Groq Llama translation API returned {StatusCode}: {ErrorBody}", response.StatusCode, responseBody);
                throw new HttpRequestException(responseBody, null, response.StatusCode);
            }

            using var doc = JsonDocument.Parse(responseBody);
            var choices = doc.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() > 0)
            {
                var content = choices[0].GetProperty("message").GetProperty("content").GetString();
                if (!string.IsNullOrWhiteSpace(content))
                {
                    return new TranslationProviderResult
                    {
                        TranslatedText = content.Trim().Trim('"'),
                        TargetLanguage = targetLang,
                        ProviderName = ProviderName,
                        ModelName = modelName,
                        LatencyMs = sw.ElapsedMilliseconds
                    };
                }
            }
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to translate text with Groq Llama.");
        }

        return null;
    }
}
