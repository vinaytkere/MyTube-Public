using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application.AI;

namespace MyTube.Backend.Infrastructure.Ai.Transcription;

public class GeminiTranscriptionProvider : ITranscriptionProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiTranscriptionProvider> _logger;

    public string ProviderName => "Gemini";

    public GeminiTranscriptionProvider(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GeminiTranscriptionProvider> logger)
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

    public async Task<TranscriptionProviderResult?> TranscribeAudioStreamAsync(Stream audioStream, string mimeType, CancellationToken cancellationToken = default)
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Gemini API key not configured for audio transcription.");
            return null;
        }

        var sw = Stopwatch.StartNew();
        var modelName = _configuration["AiOptions:LiveTranscription:GeminiModel"] ?? "gemini-2.0-flash";

        try
        {
            byte[] streamBytes;
            if (audioStream is MemoryStream ms)
            {
                streamBytes = ms.ToArray();
            }
            else
            {
                using var tempMs = new MemoryStream();
                await audioStream.CopyToAsync(tempMs, cancellationToken);
                streamBytes = tempMs.ToArray();
            }

            if (streamBytes.Length == 0) return null;

            string base64Data = Convert.ToBase64String(streamBytes);
            string cleanMime = string.IsNullOrWhiteSpace(mimeType) ? "audio/webm" : mimeType;

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
                                    mime_type = cleanMime,
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
                _logger.LogWarning("Gemini audio transcription API returned {StatusCode}: {ErrorBody}", response.StatusCode, responseJson);
                throw new HttpRequestException(responseJson, null, response.StatusCode);
            }

            using var doc = JsonDocument.Parse(responseJson);
            var candidates = doc.RootElement.GetProperty("candidates");
            if (candidates.GetArrayLength() > 0)
            {
                var parts = candidates[0].GetProperty("content").GetProperty("parts");
                if (parts.GetArrayLength() > 0)
                {
                    var text = parts[0].GetProperty("text").GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return new TranscriptionProviderResult
                        {
                            Text = text.Trim(),
                            DetectedLanguage = "en",
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
            _logger.LogError(ex, "Gemini audio transcription failed.");
        }

        return null;
    }
}
