using System;
using System.Net.Http;

namespace MyTube.Backend.Infrastructure.Servarr;

/// <summary>
/// Common HTTP client and authentication helpers for Servarr services (Radarr, Sonarr, Prowlarr).
/// </summary>
public static class ServarrClientHelper
{
    public static void ConfigureClient(HttpClient client, string baseUrl, string? apiKey, TimeSpan? timeout = null)
    {
        var normalizedBaseUrl = (baseUrl ?? string.Empty).Trim().TrimEnd('/') + "/";
        if (Uri.TryCreate(normalizedBaseUrl, UriKind.Absolute, out var uri))
        {
            client.BaseAddress = uri;
        }

        try
        {
            client.Timeout = timeout ?? TimeSpan.FromSeconds(25);
        }
        catch (InvalidOperationException)
        {
            // Timeout cannot be modified once requests have started on shared client
        }

        if (!string.IsNullOrWhiteSpace(apiKey) && !client.DefaultRequestHeaders.Contains("X-Api-Key"))
        {
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey.Trim().Trim('"', '\''));
        }
    }

    public static string MaskApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return string.Empty;
        var trimmed = apiKey.Trim();
        var prefixLength = Math.Min(4, trimmed.Length);
        return $"{trimmed[..prefixLength]}****";
    }

    public static string NormalizeBaseUrl(string baseUrl)
    {
        return (baseUrl ?? string.Empty).Trim().TrimEnd('/');
    }
}
