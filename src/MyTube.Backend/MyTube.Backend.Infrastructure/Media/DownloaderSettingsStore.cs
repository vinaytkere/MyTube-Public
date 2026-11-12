using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application;

namespace MyTube.Backend.Infrastructure.Media;

public class DownloaderSettingsStore : IDownloaderSettingsStore
{
    private readonly IConfiguration _configuration;
    private readonly IMediaToolRunner _toolRunner;
    private readonly ILogger<DownloaderSettingsStore> _logger;
    private readonly string _mediaRoot;
    private readonly object _lock = new();

    private DownloaderSettingsDto _settings;
    private string? _cachedYtDlpVersion;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public DownloaderSettingsStore(
        IConfiguration configuration,
        IMediaToolRunner toolRunner,
        ILogger<DownloaderSettingsStore> logger)
    {
        _configuration = configuration;
        _toolRunner = toolRunner;
        _logger = logger;
        _mediaRoot = configuration["Media:RootPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "media");

        _settings = new DownloaderSettingsDto
        {
            GeoBypass = configuration["Media:GeoBypass"] is null || !bool.TryParse(configuration["Media:GeoBypass"], out var gb) || gb,
            Xff = configuration["Media:Xff"] ?? "default",
            GeoVerificationProxy = configuration["Media:GeoVerificationProxy"],
            Proxy = configuration["Media:Proxy"],
            ForceIpv4 = configuration["Media:ForceIpv4"] is null || !bool.TryParse(configuration["Media:ForceIpv4"], out var f4) || f4,
            CookiesPath = configuration["Media:CookiesPath"],
            ExtractorArgs = configuration["Media:ExtractorArgs"] ?? YtDlpBypassHelper.DefaultExtractorArgs
        };

        UpdateCookiesStatus();
    }

    public DownloaderSettingsDto GetSettings()
    {
        lock (_lock)
        {
            UpdateCookiesStatus();

            return new DownloaderSettingsDto
            {
                GeoBypass = _settings.GeoBypass,
                Xff = _settings.Xff,
                GeoVerificationProxy = _settings.GeoVerificationProxy,
                Proxy = _settings.Proxy,
                ForceIpv4 = _settings.ForceIpv4,
                CookiesPath = _settings.CookiesPath,
                HasCookiesFile = _settings.HasCookiesFile,
                CookiesResolvedPath = _settings.CookiesResolvedPath,
                ExtractorArgs = _settings.ExtractorArgs,
                YtDlpVersion = _cachedYtDlpVersion ?? TryGetYtDlpVersion()
            };
        }
    }

    public async Task UpdateSettingsAsync(DownloaderSettingsDto settings, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _settings.GeoBypass = settings.GeoBypass;
            _settings.Xff = string.IsNullOrWhiteSpace(settings.Xff) ? "default" : settings.Xff.Trim();
            _settings.GeoVerificationProxy = string.IsNullOrWhiteSpace(settings.GeoVerificationProxy) ? null : settings.GeoVerificationProxy.Trim();
            _settings.Proxy = string.IsNullOrWhiteSpace(settings.Proxy) ? null : settings.Proxy.Trim();
            _settings.ForceIpv4 = settings.ForceIpv4;
            _settings.CookiesPath = string.IsNullOrWhiteSpace(settings.CookiesPath) ? null : settings.CookiesPath.Trim();
            _settings.ExtractorArgs = string.IsNullOrWhiteSpace(settings.ExtractorArgs) ? YtDlpBypassHelper.DefaultExtractorArgs : settings.ExtractorArgs.Trim();

            UpdateCookiesStatus();
        }

        try
        {
            var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(appSettingsPath))
            {
                var json = await File.ReadAllTextAsync(appSettingsPath, cancellationToken);
                var rootNode = JsonNode.Parse(json);
                if (rootNode is JsonObject rootObj)
                {
                    if (rootObj["Media"] is not JsonObject mediaObj)
                    {
                        mediaObj = new JsonObject();
                        rootObj["Media"] = mediaObj;
                    }

                    mediaObj["GeoBypass"] = _settings.GeoBypass;
                    mediaObj["Xff"] = _settings.Xff;
                    mediaObj["GeoVerificationProxy"] = _settings.GeoVerificationProxy ?? "";
                    mediaObj["Proxy"] = _settings.Proxy ?? "";
                    mediaObj["ForceIpv4"] = _settings.ForceIpv4;
                    mediaObj["CookiesPath"] = _settings.CookiesPath ?? "";
                    mediaObj["ExtractorArgs"] = _settings.ExtractorArgs ?? "";

                    // Clean up any legacy flattened keys if they exist
                    rootObj.Remove("Media:GeoBypass");
                    rootObj.Remove("Media:Xff");
                    rootObj.Remove("Media:GeoVerificationProxy");
                    rootObj.Remove("Media:Proxy");
                    rootObj.Remove("Media:ForceIpv4");
                    rootObj.Remove("Media:CookiesPath");
                    rootObj.Remove("Media:ExtractorArgs");

                    var updatedJson = rootObj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(appSettingsPath, updatedJson, cancellationToken);
                    _logger.LogInformation("Downloader geo/restriction bypass settings saved to appsettings.json.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist downloader settings to appsettings.json. Settings kept in-memory.");
        }
    }

    private void UpdateCookiesStatus()
    {
        var resolved = YtDlpBypassHelper.ResolveCookiesPath(_settings.CookiesPath, _mediaRoot);
        _settings.HasCookiesFile = resolved != null;
        _settings.CookiesResolvedPath = resolved;
    }

    private string? TryGetYtDlpVersion()
    {
        try
        {
            var res = _toolRunner.RunToolAsync("yt-dlp", new[] { "--version" }, timeout: TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            if (res.ExitCode == 0 && !string.IsNullOrWhiteSpace(res.StandardOutput))
            {
                _cachedYtDlpVersion = res.StandardOutput.Trim();
                return _cachedYtDlpVersion;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not retrieve yt-dlp version synchronously.");
        }
        return null;
    }
}
