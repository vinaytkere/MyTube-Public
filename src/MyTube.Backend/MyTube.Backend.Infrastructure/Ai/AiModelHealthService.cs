using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application;
using MyTube.Backend.Domain;

namespace MyTube.Backend.Infrastructure.Ai;

public class AiModelHealthService : IAiModelHealthService
{
    private readonly VideoDbContext _db;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AiModelHealthService> _logger;

    public AiModelHealthService(
        VideoDbContext db,
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<AiModelHealthService> logger)
    {
        _db = db;
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    private string? GetApiKey(string provider)
    {
        if (provider.Equals("Groq", StringComparison.OrdinalIgnoreCase))
        {
            var key = Environment.GetEnvironmentVariable("GROQ_API_KEY");
            if (string.IsNullOrWhiteSpace(key)) key = Environment.GetEnvironmentVariable("Groq__ApiKey");
            if (string.IsNullOrWhiteSpace(key)) key = _configuration["GROQ_API_KEY"];
            if (string.IsNullOrWhiteSpace(key)) key = _configuration["Groq:ApiKey"];
            if (string.IsNullOrWhiteSpace(key)) key = _configuration["Groq__ApiKey"];
            return key?.Trim();
        }
        if (provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            var key = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            if (string.IsNullOrWhiteSpace(key)) key = Environment.GetEnvironmentVariable("Gemini__ApiKey");
            if (string.IsNullOrWhiteSpace(key)) key = _configuration["GEMINI_API_KEY"];
            if (string.IsNullOrWhiteSpace(key)) key = _configuration["Gemini:ApiKey"];
            if (string.IsNullOrWhiteSpace(key)) key = _configuration["Gemini__ApiKey"];
            return key?.Trim();
        }
        if (provider.Equals("DeepSeek", StringComparison.OrdinalIgnoreCase))
        {
            var key = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
            if (string.IsNullOrWhiteSpace(key)) key = Environment.GetEnvironmentVariable("DeepSeek__ApiKey");
            if (string.IsNullOrWhiteSpace(key)) key = _configuration["DEEPSEEK_API_KEY"];
            if (string.IsNullOrWhiteSpace(key)) key = _configuration["DeepSeek:ApiKey"];
            if (string.IsNullOrWhiteSpace(key)) key = _configuration["DeepSeek__ApiKey"];
            return key?.Trim();
        }
        return null;
    }

    public async Task ValidateAndSyncProviderModelsAsync(string provider)
    {
        var apiKey = GetApiKey(provider);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning($"Skipping model health validation for provider {provider}: API key not configured.");
            return;
        }

        try
        {
            var activeModelIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (provider.Equals("Groq", StringComparison.OrdinalIgnoreCase))
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.groq.com/openai/v1/models");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("data", out var dataArr))
                    {
                        foreach (var item in dataArr.EnumerateArray())
                        {
                            if (item.TryGetProperty("id", out var idElem))
                            {
                                var idStr = idElem.GetString();
                                if (!string.IsNullOrWhiteSpace(idStr)) activeModelIds.Add(idStr);
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogWarning($"Groq model discovery endpoint returned status {response.StatusCode}");
                }
            }
            else if (provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
            {
                var requestUrl = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
                using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("models", out var modelsArr))
                    {
                        foreach (var item in modelsArr.EnumerateArray())
                        {
                            if (item.TryGetProperty("name", out var nameElem))
                            {
                                var rawName = nameElem.GetString();
                                if (!string.IsNullOrWhiteSpace(rawName))
                                {
                                    var cleanName = rawName.StartsWith("models/") ? rawName.Substring(7) : rawName;
                                    activeModelIds.Add(cleanName);
                                }
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogWarning($"Gemini model discovery endpoint returned status {response.StatusCode}");
                }
            }

            if (activeModelIds.Count == 0) return;

            var now = DateTime.UtcNow;
            var dbQuotas = await _db.AiModelQuotas
                .Where(q => q.Provider.ToLower() == provider.ToLower())
                .ToListAsync();
            var existingModelNames = new HashSet<string>(dbQuotas.Select(q => q.ModelName), StringComparer.OrdinalIgnoreCase);
            int maxPriority = dbQuotas.Count > 0 ? dbQuotas.Max(q => q.PriorityOrder) : 0;

            foreach (var quota in dbQuotas)
            {
                quota.LastCheckedAt = now;
                if (activeModelIds.Contains(quota.ModelName))
                {
                    if (quota.Status == "ModelNotFound")
                    {
                        quota.Status = "Active";
                        quota.IsEnabled = true;
                        quota.LastErrorDetails = null;
                        _logger.LogInformation($"Model {quota.ModelName} ({quota.Provider}) re-validated as active.");
                    }
                }
                else
                {
                    // Model not listed in provider's active API models list
                    quota.Status = "ModelNotFound";
                    quota.IsEnabled = false;
                    quota.LastErrorDetails = "Model identifier not listed in provider API discovery endpoint.";
                    _logger.LogWarning($"Model {quota.ModelName} ({quota.Provider}) not found in active provider discovery list. Flagged as ModelNotFound (Disabled).");
                }
            }

            // Auto-insert any newly discovered active text/chat models that are not yet in the DB
            foreach (var modelId in activeModelIds)
            {
                if (!existingModelNames.Contains(modelId))
                {
                    bool isEligibleTextModel = provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase)
                        ? (modelId.StartsWith("gemini", StringComparison.OrdinalIgnoreCase) && !modelId.Contains("embedding") && !modelId.Contains("imagen") && !modelId.Contains("aqa"))
                        : (modelId.StartsWith("llama", StringComparison.OrdinalIgnoreCase) || modelId.StartsWith("mixtral", StringComparison.OrdinalIgnoreCase) || modelId.StartsWith("gemma", StringComparison.OrdinalIgnoreCase) || modelId.StartsWith("qwen", StringComparison.OrdinalIgnoreCase) || modelId.StartsWith("deepseek", StringComparison.OrdinalIgnoreCase));

                    if (isEligibleTextModel)
                    {
                        maxPriority++;
                        var newQuota = new AiModelQuota
                        {
                            Provider = provider,
                            ModelName = modelId,
                            RpmLimit = provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase) ? 15 : 30,
                            RpdLimit = provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase) ? 1500 : 1000,
                            TaskType = "Text",
                            PriorityOrder = maxPriority,
                            Status = "Active",
                            IsEnabled = true,
                            LastCheckedAt = now,
                            WindowStartMinute = now,
                            WindowStartDay = now.Date
                        };
                        _db.AiModelQuotas.Add(newQuota);
                        existingModelNames.Add(modelId);
                        _logger.LogInformation($"Auto-discovered and registered new {provider} model: {modelId}");
                    }
                }
            }

            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error validating provider models for {provider}");
        }
    }

    public async Task HandleModelNotFoundAsync(int quotaId, string modelName)
    {
        var quota = await _db.AiModelQuotas.FindAsync(quotaId);
        if (quota != null)
        {
            quota.Status = "ModelNotFound";
            quota.IsEnabled = false;
            quota.LastCheckedAt = DateTime.UtcNow;
            quota.LastErrorDetails = $"API returned HTTP 404 (Model Not Found) on {DateTime.UtcNow:u}";
            await _db.SaveChangesAsync();
            _logger.LogWarning($"Model ID {quota.Id} ({quota.Provider} - {quota.ModelName}) marked as ModelNotFound and disabled. Historical DB row preserved.");

            // Probe provider discovery in background to refresh active lists
            await ValidateAndSyncProviderModelsAsync(quota.Provider);
        }
    }
}
