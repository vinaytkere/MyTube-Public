using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application.Security;

namespace MyTube.Backend.Infrastructure.Ai;

public class FirecrawlService : Application.AI.IFirecrawlService
{
    private readonly HttpClient _httpClient;
    private readonly ISecretManagementService _secretService;
    private readonly ILogger<FirecrawlService> _logger;
    private const string SearchEndpoint = "https://api.firecrawl.dev/v1/search";

    public FirecrawlService(
        HttpClient httpClient, 
        ISecretManagementService secretService,
        ILogger<FirecrawlService> logger)
    {
        _httpClient = httpClient;
        _secretService = secretService;
        _logger = logger;
    }

    public async Task<string> SearchWebContextAsync(string query)
    {
        try
        {
            var apiKey = await _secretService.GetSecretAsync("Firecrawl:ApiKey");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogDebug("Firecrawl API key is not configured. Skipping web context search.");
                return string.Empty;
            }

            _logger.LogInformation("Executing Firecrawl search query: {Query}", query);

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, SearchEndpoint);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var body = new
            {
                query = query,
                limit = 3
            };

            var jsonContent = JsonSerializer.Serialize(body);
            requestMessage.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(requestMessage);
            if (!response.IsSuccessStatusCode)
            {
                var errText = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Firecrawl search failed with status {StatusCode}: {Error}", response.StatusCode, errText);
                return string.Empty;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var dataArray) && dataArray.ValueKind == JsonValueKind.Array)
            {
                var snippets = new List<string>();
                foreach (var item in dataArray.EnumerateArray())
                {
                    var title = CleanText(item.TryGetProperty("title", out var t) ? t.GetString() : "");
                    var url = CleanText(item.TryGetProperty("url", out var u) ? u.GetString() : "");
                    var description = CleanText(item.TryGetProperty("description", out var d) ? d.GetString() : "");
                    var markdown = CleanText(item.TryGetProperty("markdown", out var m) ? m.GetString() : "");

                    var content = !string.IsNullOrWhiteSpace(markdown) 
                        ? (markdown.Length > 800 ? markdown.Substring(0, 800) + "..." : markdown)
                        : description;

                    snippets.Add($"Source [{title}]({url}): {content}");
                }

                return CleanText(string.Join("\n\n", snippets));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Firecrawl API call exception for query: {Query}", query);
        }

        return string.Empty;
    }

    private static string CleanText(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return input.Replace("\0", string.Empty).Replace("\u0000", string.Empty);
    }
}
