using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application.AI;

namespace MyTube.Backend.Infrastructure.Ai;

public class SmartRouteClient : ISmartRouteClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmartRouteClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SmartRouteClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<SmartRouteClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        var baseUrl = _configuration["SmartRoute:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            if (!baseUrl.EndsWith('/')) baseUrl += "/";
            _httpClient.BaseAddress = new Uri(baseUrl);
        }

        var timeoutSec = _configuration.GetValue<int?>("SmartRoute:TimeoutSeconds") ?? 60;
        _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSec);
    }

    public Task<MediaClassificationResult?> ClassifyAndSummarizeMediaAsync(
        string title,
        string? uploader,
        string? description,
        string? subtitleExcerpt,
        CancellationToken cancellationToken = default)
    {
        return ClassifyMediaWithEvidenceAsync(title, uploader, description, subtitleExcerpt, null, cancellationToken);
    }

    public async Task<MediaClassificationResult?> ClassifyMediaWithEvidenceAsync(
        string title,
        string? uploader,
        string? description,
        string? subtitleExcerpt,
        string? mediaEvidence,
        CancellationToken cancellationToken = default)
    {
        if (_httpClient.BaseAddress == null)
        {
            _logger.LogInformation("SmartRoute BaseUrl is not configured; skipping remote AI classification.");
            return null;
        }

        try
        {
            var systemPrompt = 
                "You are an expert Media & Movie Organizer AI conforming to the SmartRoute Media Organizer specification.\n" +
                "Your objective is to classify media into a clean, consistent library and identify its primary category, language, canonical title, release year, artist, and summary.\n\n" +
                "PRIMARY CLASSIFICATION RULES:\n" +
                "Classify each file into EXACTLY ONE primary media category:\n" +
                "Video Categories:\n" +
                "- Movies\n" +
                "- TV Shows\n" +
                "- Web Series\n" +
                "- Documentaries\n" +
                "- Short Films\n" +
                "- Comedy\n" +
                "- Educational\n" +
                "- Music Videos\n" +
                "- Personal Videos\n" +
                "- Unclassified\n\n" +
                "Audio Categories:\n" +
                "- Songs\n" +
                "- Podcasts\n" +
                "- Audiobooks\n" +
                "- Devotional\n" +
                "- Classical\n" +
                "- Comedy\n" +
                "- Soundtracks\n" +
                "- Recordings\n" +
                "- Unclassified\n\n" +
                "CRITICAL RULES:\n" +
                "- Do NOT invent composite category folders such as 'Audio - Carnatic', 'Audio - Folk', 'Movies - Action', 'Drama'. Category MUST be strictly one of the 10 Video or 9 Audio categories above.\n" +
                "- A Kannada devotional song belongs to Category 'Devotional' (mediaType 'Audio') with language 'Kannada'.\n" +
                "- A Carnatic vocal recording belongs to Category 'Classical' (mediaType 'Audio') with primaryGenre 'Carnatic'.\n" +
                "- A Kannada historical documentary belongs to Category 'Documentaries' (mediaType 'Video') with language 'Kannada'.\n" +
                "- A movie containing comedy belongs to Category 'Movies' with primaryGenre 'Comedy'.\n" +
                "- When year or language cannot be identified with high confidence, leave it null/empty instead of inventing.\n\n" +
                "You must respond ONLY with valid JSON with no markdown wrapping, matching this exact schema:\n" +
                "{\n" +
                "  \"mediaType\": \"Video\" | \"Audio\",\n" +
                "  \"category\": \"Movies|TV Shows|Web Series|Documentaries|Short Films|Comedy|Educational|Music Videos|Personal Videos|Songs|Podcasts|Audiobooks|Devotional|Classical|Soundtracks|Recordings|Unclassified\",\n" +
                "  \"canonicalTitle\": \"Official clean title stripped of junk, resolution, release groups, and clickbait\",\n" +
                "  \"language\": \"Kannada|English|Hindi|Tamil|Telugu|Malayalam|Sanskrit|etc\",\n" +
                "  \"releaseYear\": 2022,\n" +
                "  \"artist\": \"Artist, singer, director, or creator if verified\",\n" +
                "  \"seasonEpisode\": \"S01E01\" (if TV Show or Web Series),\n" +
                "  \"part\": \"Part01\" (if Audiobook or Series),\n" +
                "  \"primaryGenre\": \"Specific genre (e.g. Action, Sci-Fi, Carnatic, Devotional, Soundtrack, Tech)\",\n" +
                "  \"summary\": \"Concise 2-3 sentence synopsis or summary of content.\",\n" +
                "  \"confidence\": 0.95,\n" +
                "  \"classificationReason\": \"Key evidence used (e.g. embedded tags, filename, duration)\"\n" +
                "}";

            var userPromptBuilder = new System.Text.StringBuilder();
            userPromptBuilder.AppendLine($"Title: {title}");
            if (!string.IsNullOrWhiteSpace(uploader)) userPromptBuilder.AppendLine($"Uploader/Channel/Artist: {uploader}");
            if (!string.IsNullOrWhiteSpace(mediaEvidence))
            {
                userPromptBuilder.AppendLine("Harvested Technical & Embedded Evidence:\n" + mediaEvidence);
            }
            if (!string.IsNullOrWhiteSpace(description))
            {
                var trimmedDesc = description.Length > 2000 ? description.Substring(0, 2000) + "..." : description;
                userPromptBuilder.AppendLine($"Description:\n{trimmedDesc}");
            }
            if (!string.IsNullOrWhiteSpace(subtitleExcerpt))
            {
                var trimmedSubs = subtitleExcerpt.Length > 3000 ? subtitleExcerpt.Substring(0, 3000) + "..." : subtitleExcerpt;
                userPromptBuilder.AppendLine($"Subtitles Excerpt:\n{trimmedSubs}");
            }

            var requestBody = new
            {
                prompt = new
                {
                    systemPrompt = systemPrompt,
                    userPrompt = userPromptBuilder.ToString(),
                    maxOutputTokens = 800,
                    temperature = 0.2
                },
                criteria = new
                {
                    preferredBillingTier = 1 // Enforce Free Tier (Groq, Cerebras, Gemini) - excludes DeepSeek (Paid)
                }
            };

            _logger.LogInformation("Sending classification request to OCI SmartRoute for media '{Title}'...", title);

            var response = await _httpClient.PostAsJsonAsync("api/smartroute/chat", requestBody, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("OCI SmartRoute returned error {StatusCode}: {Error}", response.StatusCode, errorContent);
                return null;
            }

            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("success", out var succProp) && succProp.GetBoolean() &&
                root.TryGetProperty("content", out var contentProp))
            {
                var content = contentProp.GetString();
                if (string.IsNullOrWhiteSpace(content)) return null;

                // Strip any markdown fences if model included them
                content = CleanJsonMarkdown(content);

                var classification = JsonSerializer.Deserialize<MediaClassificationResult>(content, JsonOptions);
                if (classification != null)
                {
                    var modelUsed = root.TryGetProperty("selectedModelIdentifier", out var mi) ? mi.GetString() : "unknown";
                    var providerUsed = root.TryGetProperty("selectedProviderName", out var pi) ? pi.GetString() : "unknown";
                    _logger.LogInformation("OCI SmartRoute classified '{Title}' as '{Category}' ({Language}) using {Provider}:{Model}.",
                        title, classification.Category, classification.Language, providerUsed, modelUsed);
                    return classification;
                }
            }
            else if (root.TryGetProperty("errorMessage", out var errProp) && !string.IsNullOrEmpty(errProp.GetString()))
            {
                _logger.LogWarning("OCI SmartRoute error message: {Error}", errProp.GetString());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to classify media via OCI SmartRoute for title '{Title}'.", title);
        }

        return null;
    }

    private static string CleanJsonMarkdown(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring(7);
        }
        else if (trimmed.StartsWith("```"))
        {
            trimmed = trimmed.Substring(3);
        }

        if (trimmed.EndsWith("```"))
        {
            trimmed = trimmed.Substring(0, trimmed.Length - 3);
        }

        return trimmed.Trim();
    }
}
