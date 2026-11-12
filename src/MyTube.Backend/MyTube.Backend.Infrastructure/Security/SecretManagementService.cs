using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application.Security;

namespace MyTube.Backend.Infrastructure.Security;

/// <summary>
/// Single point of responsibility for fetching secrets and credentials.
/// Integrates Infisical vault with seamless fallbacks to Environment variables and ASP.NET Core IConfiguration.
/// Features thread-safe in-memory caching with configurable TTL to avoid excessive network overhead.
/// </summary>
public class SecretManagementService : ISecretManagementService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SecretManagementService> _logger;

    private readonly ConcurrentDictionary<string, CachedSecret> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _cacheTtl;

    private readonly string? _infisicalBaseUrl;
    private readonly string? _infisicalToken;
    private readonly string? _infisicalClientId;
    private readonly string? _infisicalClientSecret;
    private readonly string? _infisicalProjectId;
    private readonly string _infisicalEnvironment;
    private readonly string _secretsPath;

    private string? _cachedUniversalAuthToken;
    private DateTimeOffset _universalAuthTokenExpiresAt = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _authLock = new(1, 1);

    private record CachedSecret(string Value, DateTimeOffset ExpiresAt);

    public SecretManagementService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<SecretManagementService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;

        _secretsPath = _configuration["SecretsPath"] 
            ?? Environment.GetEnvironmentVariable("SECRETS_PATH") 
            ?? "/run/secrets";

        var ttlSeconds = _configuration.GetValue<int?>("Infisical:CacheTtlSeconds") ?? 300;
        _cacheTtl = TimeSpan.FromSeconds(Math.Max(30, ttlSeconds));

        _infisicalBaseUrl = (_configuration["Infisical:BaseUrl"] 
            ?? Environment.GetEnvironmentVariable("INFISICAL_BASE_URL") 
            ?? "https://app.infisical.com").TrimEnd('/');

        _infisicalToken = _configuration["Infisical:Token"] 
            ?? Environment.GetEnvironmentVariable("INFISICAL_TOKEN");

        _infisicalClientId = _configuration["Infisical:ClientId"] 
            ?? Environment.GetEnvironmentVariable("INFISICAL_CLIENT_ID");

        _infisicalClientSecret = _configuration["Infisical:ClientSecret"] 
            ?? Environment.GetEnvironmentVariable("INFISICAL_CLIENT_SECRET");

        _infisicalProjectId = _configuration["Infisical:ProjectId"] 
            ?? _configuration["Infisical:WorkspaceId"]
            ?? Environment.GetEnvironmentVariable("INFISICAL_PROJECT_ID")
            ?? Environment.GetEnvironmentVariable("INFISICAL_WORKSPACE_ID");

        _infisicalEnvironment = _configuration["Infisical:Environment"] 
            ?? Environment.GetEnvironmentVariable("INFISICAL_ENVIRONMENT") 
            ?? "dev";
    }

    public async Task<string?> GetSecretAsync(string secretKey, string? defaultValue = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
            return defaultValue;

        // 1. Check local in-memory cache
        if (_cache.TryGetValue(secretKey, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached.Value;
        }

        // 2. Highest Priority: Docker Compose file-based secrets (/run/secrets/<secret_name>)
        var dockerSecret = GetFromDockerSecrets(secretKey);
        if (!string.IsNullOrEmpty(dockerSecret))
        {
            _cache[secretKey] = new CachedSecret(dockerSecret, DateTimeOffset.UtcNow.Add(_cacheTtl));
            return dockerSecret;
        }

        // 3. Environment Variables (documented fallback)
        var envValue = GetFromEnvironment(secretKey);
        if (!string.IsNullOrEmpty(envValue))
        {
            _cache[secretKey] = new CachedSecret(envValue, DateTimeOffset.UtcNow.Add(_cacheTtl));
            return envValue;
        }

        // 4. IConfiguration (appsettings.json)
        var configValue = _configuration[secretKey];
        if (!string.IsNullOrEmpty(configValue))
        {
            _cache[secretKey] = new CachedSecret(configValue, DateTimeOffset.UtcNow.Add(_cacheTtl));
            return configValue;
        }

        // 5. Attempt fetching from Infisical if credentials are configured
        if (IsInfisicalConfigured())
        {
            try
            {
                var infisicalValue = await FetchFromInfisicalAsync(secretKey, cancellationToken);
                if (!string.IsNullOrEmpty(infisicalValue))
                {
                    _cache[secretKey] = new CachedSecret(infisicalValue, DateTimeOffset.UtcNow.Add(_cacheTtl));
                    return infisicalValue;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to retrieve secret '{SecretKey}' from Infisical. Reason: {Message}",
                    secretKey, ex.Message);
            }
        }

        // 6. Fallback to supplied default value
        if (defaultValue != null)
        {
            _cache[secretKey] = new CachedSecret(defaultValue, DateTimeOffset.UtcNow.Add(_cacheTtl));
            return defaultValue;
        }

        return null;
    }

    public string? GetSecret(string secretKey, string? defaultValue = null)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
            return defaultValue;

        if (_cache.TryGetValue(secretKey, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached.Value;
        }

        // 1. Highest Priority: Docker Compose file-based secrets
        var dockerSecret = GetFromDockerSecrets(secretKey);
        if (!string.IsNullOrEmpty(dockerSecret))
        {
            _cache[secretKey] = new CachedSecret(dockerSecret, DateTimeOffset.UtcNow.Add(_cacheTtl));
            return dockerSecret;
        }

        // 2. Environment variables
        var envValue = GetFromEnvironment(secretKey);
        if (!string.IsNullOrEmpty(envValue))
        {
            _cache[secretKey] = new CachedSecret(envValue, DateTimeOffset.UtcNow.Add(_cacheTtl));
            return envValue;
        }

        // 3. IConfiguration
        var configValue = _configuration[secretKey];
        if (!string.IsNullOrEmpty(configValue))
        {
            _cache[secretKey] = new CachedSecret(configValue, DateTimeOffset.UtcNow.Add(_cacheTtl));
            return configValue;
        }

        // If Infisical is configured, invoke async safely
        if (IsInfisicalConfigured())
        {
            try
            {
                var infisicalValue = Task.Run(() => GetSecretAsync(secretKey, defaultValue)).GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(infisicalValue))
                    return infisicalValue;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Synchronous secret retrieval from Infisical failed for '{SecretKey}': {Message}", secretKey, ex.Message);
            }
        }

        if (defaultValue != null)
        {
            _cache[secretKey] = new CachedSecret(defaultValue, DateTimeOffset.UtcNow.Add(_cacheTtl));
            return defaultValue;
        }

        return null;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetSecretsAsync(IEnumerable<string> secretKeys, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in secretKeys)
        {
            var value = await GetSecretAsync(key, cancellationToken: cancellationToken);
            if (value != null)
            {
                result[key] = value;
            }
        }
        return result;
    }

    public void InvalidateCache(string? secretKey = null)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            _cache.Clear();
            _logger.LogInformation("SecretManagementService cache completely invalidated.");
        }
        else
        {
            _cache.TryRemove(secretKey, out _);
            _logger.LogDebug("SecretManagementService cache entry for '{SecretKey}' invalidated.", secretKey);
        }
    }

    private bool IsInfisicalConfigured()
    {
        return !string.IsNullOrWhiteSpace(_infisicalToken) || 
               (!string.IsNullOrWhiteSpace(_infisicalClientId) && !string.IsNullOrWhiteSpace(_infisicalClientSecret));
    }

    private string? GetFromDockerSecrets(string secretKey)
    {
        if (string.IsNullOrWhiteSpace(secretKey) || !System.IO.Directory.Exists(_secretsPath))
            return null;

        var candidates = new List<string>
        {
            secretKey,
            secretKey.Replace(":", "_"),
            secretKey.Replace(":", "__"),
            secretKey.Replace(":", "_").ToLowerInvariant(),
            System.Text.RegularExpressions.Regex.Replace(secretKey, "([a-z0-9])([A-Z])", "$1_$2").Replace(":", "_").ToLowerInvariant(),
            System.Text.RegularExpressions.Regex.Replace(secretKey, "([a-z0-9])([A-Z])", "$1_$2").Replace(":", "_").ToUpperInvariant()
        };

        if (secretKey.Contains("Jwt", StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add("jwt_secret");
            candidates.Add("jwt_key");
        }

        foreach (var name in candidates.Distinct())
        {
            var filePath = System.IO.Path.Combine(_secretsPath, name);
            if (System.IO.File.Exists(filePath))
            {
                try
                {
                    var content = System.IO.File.ReadAllText(filePath).Trim();
                    if (!string.IsNullOrEmpty(content))
                    {
                        return content;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to read secret file at {Path}: {Message}", filePath, ex.Message);
                }
            }
        }

        return null;
    }

    private string? GetFromEnvironment(string secretKey)
    {
        // 1. Try exact match
        var val = Environment.GetEnvironmentVariable(secretKey);
        if (!string.IsNullOrEmpty(val)) return val;

        // 2. Direct underscore (e.g. "Brevo:ApiKey" -> "BREVO_APIKEY")
        var simpleUnderscore = secretKey.Replace(":", "_").Replace(".", "_").ToUpperInvariant();
        val = Environment.GetEnvironmentVariable(simpleUnderscore);
        if (!string.IsNullOrEmpty(val)) return val;

        // 3. Snake case conversion (e.g. "Brevo:ApiKey" -> "BREVO_API_KEY")
        var snakeCase = System.Text.RegularExpressions.Regex.Replace(secretKey, "([a-z0-9])([A-Z])", "$1_$2")
            .Replace(":", "_").Replace(".", "_").ToUpperInvariant();
        val = Environment.GetEnvironmentVariable(snakeCase);
        if (!string.IsNullOrEmpty(val)) return val;

        // 4. Try with MYTUBE_ prefix
        val = Environment.GetEnvironmentVariable($"MYTUBE_{simpleUnderscore}")
            ?? Environment.GetEnvironmentVariable($"MYTUBE_{snakeCase}");
        if (!string.IsNullOrEmpty(val)) return val;

        // 5. Try ASP.NET Core double underscore format (e.g. "Brevo__ApiKey")
        var doubleUnderscore = secretKey.Replace(":", "__");
        val = Environment.GetEnvironmentVariable(doubleUnderscore);
        return string.IsNullOrEmpty(val) ? null : val;
    }

    private async Task<string?> FetchFromInfisicalAsync(string secretKey, CancellationToken cancellationToken)
    {
        var token = await GetInfisicalAuthTokenAsync(cancellationToken);
        if (string.IsNullOrEmpty(token)) return null;

        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_infisicalBaseUrl!);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Normalize secret name for Infisical (convert ':' to '_' or sanitize)
        var sanitizedSecretName = secretKey.Replace(":", "_").Replace(".", "_");

        var query = new List<string> { $"environment={Uri.EscapeDataString(_infisicalEnvironment)}" };
        if (!string.IsNullOrEmpty(_infisicalProjectId))
        {
            query.Add($"workspaceId={Uri.EscapeDataString(_infisicalProjectId)}");
        }

        var endpoint = $"/api/v3/secrets/raw/{Uri.EscapeDataString(sanitizedSecretName)}?{string.Join("&", query)}";

        var response = await client.GetAsync(endpoint, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var jsonDoc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (jsonDoc.RootElement.TryGetProperty("secret", out var secretElement) &&
            secretElement.TryGetProperty("secretValue", out var valElement))
        {
            return valElement.GetString();
        }

        return null;
    }

    private async Task<string?> GetInfisicalAuthTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_infisicalToken))
            return _infisicalToken;

        if (string.IsNullOrWhiteSpace(_infisicalClientId) || string.IsNullOrWhiteSpace(_infisicalClientSecret))
            return null;

        if (!string.IsNullOrWhiteSpace(_cachedUniversalAuthToken) && _universalAuthTokenExpiresAt > DateTimeOffset.UtcNow)
            return _cachedUniversalAuthToken;

        await _authLock.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(_cachedUniversalAuthToken) && _universalAuthTokenExpiresAt > DateTimeOffset.UtcNow)
                return _cachedUniversalAuthToken;

            var client = _httpClientFactory.CreateClient();
            var payload = new
            {
                clientId = _infisicalClientId,
                clientSecret = _infisicalClientSecret
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{_infisicalBaseUrl}/api/v1/auth/universal-auth/login", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var jsonDoc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (jsonDoc.RootElement.TryGetProperty("accessToken", out var tokenProp))
            {
                _cachedUniversalAuthToken = tokenProp.GetString();
                var expiresIn = jsonDoc.RootElement.TryGetProperty("expiresIn", out var expProp) ? expProp.GetInt32() : 3600;
                _universalAuthTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn - 60);
                return _cachedUniversalAuthToken;
            }

            return null;
        }
        finally
        {
            _authLock.Release();
        }
    }
}
