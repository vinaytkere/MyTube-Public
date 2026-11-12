using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application.AI;

namespace MyTube.Backend.Infrastructure.Ai.Transcription;

public class GroqWhisperProvider : ITranscriptionProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GroqWhisperProvider> _logger;

    public string ProviderName => "Groq";

    public GroqWhisperProvider(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GroqWhisperProvider> logger)
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

    public async Task<TranscriptionProviderResult?> TranscribeAudioStreamAsync(Stream audioStream, string mimeType, CancellationToken cancellationToken = default)
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Groq API key not configured for audio transcription.");
            return null;
        }

        var sw = Stopwatch.StartNew();
        var modelName = _configuration["AiOptions:LiveTranscription:GroqModel"] ?? "whisper-large-v3-turbo";

        try
        {
            using var content = new MultipartFormDataContent();
            
            // Convert stream to ByteArrayContent for stream safety
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

            if (streamBytes.Length == 0)
            {
                _logger.LogWarning("Empty audio stream received for Groq Whisper transcription.");
                return null;
            }

            var byteArrayContent = new ByteArrayContent(streamBytes);
            byteArrayContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(mimeType) ? "audio/webm" : mimeType);
            
            content.Add(byteArrayContent, "file", "chunk.webm");
            content.Add(new StringContent(modelName), "model");
            content.Add(new StringContent("verbose_json"), "response_format");

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/audio/transcriptions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = content;

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Groq Whisper API returned {StatusCode}: {ErrorBody}", response.StatusCode, responseBody);
                throw new HttpRequestException(responseBody, null, response.StatusCode);
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            string text = string.Empty;
            string language = "en";

            if (root.TryGetProperty("text", out var textElem))
            {
                text = textElem.GetString() ?? string.Empty;
            }
            if (root.TryGetProperty("language", out var langElem))
            {
                language = langElem.GetString() ?? "en";
            }

            if (string.IsNullOrWhiteSpace(text)) return null;

            return new TranscriptionProviderResult
            {
                Text = text.Trim(),
                DetectedLanguage = language,
                ProviderName = ProviderName,
                ModelName = modelName,
                LatencyMs = sw.ElapsedMilliseconds
            };
        }
        catch (HttpRequestException)
        {
            throw; // Re-throw HttpRequestException so the router can classify 429 vs 5xx vs 400
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error transcribing audio stream with Groq Whisper.");
            return null;
        }
    }
}
