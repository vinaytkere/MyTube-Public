using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MyTube.Backend.Infrastructure.Iptv;

public interface IIptvCatalogClient
{
    Task<CatalogFetchResult<T>> FetchDatasetAsync<T>(
        string url,
        string? etag = null,
        DateTime? lastModified = null,
        CancellationToken ct = default);
}

public class CatalogFetchResult<T>
{
    public bool IsNotModified { get; set; }
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }
    public string? NewETag { get; set; }
    public DateTime? NewLastModified { get; set; }
    public string? ErrorMessage { get; set; }
}

public class IptvCatalogClient : IIptvCatalogClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<IptvCatalogClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IptvCatalogClient(HttpClient httpClient, ILogger<IptvCatalogClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MyTube/1.0 (Global Free IPTV)");
        _httpClient.Timeout = TimeSpan.FromMinutes(3);
    }

    public async Task<CatalogFetchResult<T>> FetchDatasetAsync<T>(
        string url,
        string? etag = null,
        DateTime? lastModified = null,
        CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            if (!string.IsNullOrWhiteSpace(etag))
            {
                request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(etag.StartsWith("\"") ? etag : $"\"{etag}\""));
            }

            if (lastModified.HasValue)
            {
                request.Headers.IfModifiedSince = lastModified.Value;
            }

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                _logger.LogInformation("Dataset {Url} is up to date (304 Not Modified).", url);
                return new CatalogFetchResult<T>
                {
                    IsNotModified = true,
                    IsSuccess = true,
                    NewETag = etag,
                    NewLastModified = lastModified
                };
            }

            response.EnsureSuccessStatusCode();

            var newEtag = response.Headers.ETag?.Tag;
            var newLastMod = response.Content.Headers.LastModified?.UtcDateTime;

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var data = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct);

            _logger.LogInformation("Successfully downloaded dataset {Url}.", url);

            return new CatalogFetchResult<T>
            {
                IsSuccess = true,
                Data = data,
                NewETag = newEtag,
                NewLastModified = newLastMod
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch dataset from {Url}: {Message}", url, ex.Message);
            return new CatalogFetchResult<T>
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }
}

