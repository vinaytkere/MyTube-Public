using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MyTube.Backend.Application;
using MyTube.Backend.Infrastructure.Media;
using Xunit;

namespace MyTube.Backend.Tests;

public class DownloaderBypassTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _cookiesFile;

    public DownloaderBypassTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"mytube_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _cookiesFile = Path.Combine(_tempDir, "cookies.txt");
        File.WriteAllText(_cookiesFile, "# Netscape HTTP Cookie File\n.youtube.com\tTRUE\t/\tTRUE\t2147483647\tSID\tdummy_cookie_val\n");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch { }
    }

    private class FakeToolRunner : IMediaToolRunner
    {
        public string GetYtDlpPath() => "yt-dlp";
        public string GetFfmpegPath() => "ffmpeg";
        public string GetFfprobePath() => "ffprobe";

        public Task<ToolExecutionResult> RunToolAsync(
            string toolName,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken = default,
            TimeSpan? timeout = null,
            Action<string>? onStandardOutputLine = null,
            Action<string>? onStandardErrorLine = null,
            string? workingDirectory = null)
        {
            if (toolName == "yt-dlp" && arguments.Contains("--version"))
            {
                return Task.FromResult(new ToolExecutionResult(0, "2026.01.15\n", ""));
            }
            return Task.FromResult(new ToolExecutionResult(0, "{}", ""));
        }
    }

    [Fact]
    public void ApplyRestrictionBypassArgs_DefaultSettings_AppliesAllBypassFlags()
    {
        var args = new List<string> { "-J", "--no-playlist" };

        var settings = new DownloaderSettingsDto
        {
            GeoBypass = true,
            Xff = "default",
            ForceIpv4 = true,
            ExtractorArgs = "youtube:skip=dash"
        };

        YtDlpBypassHelper.ApplyRestrictionBypassArgs(args, settings);

        Assert.Contains("--geo-bypass", args);
        Assert.Contains("--xff", args);
        var xffIdx = args.IndexOf("--xff");
        Assert.Equal("default", args[xffIdx + 1]);

        Assert.Contains("--force-ipv4", args);
        Assert.Contains("--extractor-args", args);
        var extIdx = args.IndexOf("--extractor-args");
        Assert.Equal("youtube:skip=dash", args[extIdx + 1]);

        Assert.Contains("--socket-timeout", args);
        var timeoutIdx = args.IndexOf("--socket-timeout");
        Assert.Equal("30", args[timeoutIdx + 1]);
    }

    [Fact]
    public void CleanExtractorArgs_RemovesLegacyRestrictivePlayerClientsAndSkips()
    {
        var cleaned = YtDlpBypassHelper.CleanExtractorArgs("youtube:player_client=ios,android,web;player_skip=configs,webpage");
        Assert.Equal("", cleaned);

        var cleanedAndroid = YtDlpBypassHelper.CleanExtractorArgs("youtube:player_client=android,web");
        Assert.Equal("", cleanedAndroid);

        var args = new List<string>();
        YtDlpBypassHelper.ApplyRestrictionBypassArgs(args, new DownloaderSettingsDto
        {
            ExtractorArgs = "youtube:player_client=android,web;player_skip=configs,webpage"
        });
        Assert.DoesNotContain("--extractor-args", args);
    }

    [Fact]
    public void ApplyRestrictionBypassArgs_CustomCountryXffAndProxies_AppliesCorrectArguments()
    {
        var args = new List<string> { "-o", "test.mp4" };

        var settings = new DownloaderSettingsDto
        {
            GeoBypass = true,
            Xff = "US",
            GeoVerificationProxy = "http://verify-proxy.example.com:8080",
            Proxy = "socks5://download-proxy.example.com:1080",
            ForceIpv4 = true
        };

        YtDlpBypassHelper.ApplyRestrictionBypassArgs(args, settings);

        Assert.Contains("--geo-bypass", args);
        Assert.Contains("--xff", args);
        Assert.Equal("US", args[args.IndexOf("--xff") + 1]);

        Assert.Contains("--geo-verification-proxy", args);
        Assert.Equal("http://verify-proxy.example.com:8080", args[args.IndexOf("--geo-verification-proxy") + 1]);

        Assert.Contains("--proxy", args);
        Assert.Equal("socks5://download-proxy.example.com:1080", args[args.IndexOf("--proxy") + 1]);
    }

    [Fact]
    public void ApplyRestrictionBypassArgs_WhenCookiesFileExists_InjectsCookiesFlag()
    {
        var args = new List<string> { "-o", "output.mp4" };

        var settings = new DownloaderSettingsDto
        {
            CookiesPath = _cookiesFile
        };

        YtDlpBypassHelper.ApplyRestrictionBypassArgs(args, settings, mediaRoot: _tempDir);

        Assert.Contains("--cookies", args);
        Assert.Equal(Path.GetFullPath(_cookiesFile), args[args.IndexOf("--cookies") + 1]);
    }

    [Fact]
    public void ApplyRestrictionBypassArgs_WhenGeoBypassDisabled_DoesNotIncludeGeoBypassOrXff()
    {
        var args = new List<string> { "-o", "test.mp4" };

        var settings = new DownloaderSettingsDto
        {
            GeoBypass = false,
            Xff = "never"
        };

        YtDlpBypassHelper.ApplyRestrictionBypassArgs(args, settings);

        Assert.DoesNotContain("--geo-bypass", args);
        Assert.DoesNotContain("--xff", args);
    }

    [Fact]
    public async Task DownloaderSettingsStore_GetAndSet_UpdatesAndReflectsSettings()
    {
        var inMemoryConfig = new Dictionary<string, string?>
        {
            { "Media:GeoBypass", "true" },
            { "Media:Xff", "default" },
            { "Media:RootPath", _tempDir }
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig)
            .Build();

        var store = new DownloaderSettingsStore(
            config, 
            new FakeToolRunner(), 
            NullLogger<DownloaderSettingsStore>.Instance);

        var current = store.GetSettings();
        Assert.True(current.GeoBypass);
        Assert.Equal("default", current.Xff);
        Assert.True(current.HasCookiesFile);
        Assert.Equal("2026.01.15", current.YtDlpVersion);

        await store.UpdateSettingsAsync(new DownloaderSettingsDto
        {
            GeoBypass = true,
            Xff = "GB",
            GeoVerificationProxy = "http://uk-proxy:8080",
            Proxy = "http://gluetun:8888",
            ForceIpv4 = false,
            CookiesPath = _cookiesFile
        });

        var updated = store.GetSettings();
        Assert.True(updated.GeoBypass);
        Assert.Equal("GB", updated.Xff);
        Assert.Equal("http://uk-proxy:8080", updated.GeoVerificationProxy);
        Assert.Equal("http://gluetun:8888", updated.Proxy);
        Assert.False(updated.ForceIpv4);
        Assert.True(updated.HasCookiesFile);
    }

    [Fact]
    public async Task DownloaderSettingsStore_UpdateSettingsAsync_SavesToAppSettingsJsonWithoutDuplicateKeys()
    {
        var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var initialJson = """
        {
          "AllowedHosts": "*",
          "Media": {
            "RootPath": "./media",
            "GeoBypass": true,
            "Xff": "default"
          },
          "Media:GeoBypass": "True"
        }
        """;
        await File.WriteAllTextAsync(appSettingsPath, initialJson);

        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { { "Media:RootPath", _tempDir } })
                .Build();

            var store = new DownloaderSettingsStore(
                config,
                new FakeToolRunner(),
                NullLogger<DownloaderSettingsStore>.Instance);

            await store.UpdateSettingsAsync(new DownloaderSettingsDto
            {
                GeoBypass = false,
                Xff = "custom-xff",
                ForceIpv4 = true
            });

            // Ensure ConfigurationBuilder can load the updated appsettings.json without FormatException (no duplicate keys)
            var reloadedConfig = new ConfigurationBuilder()
                .AddJsonFile(appSettingsPath, optional: false, reloadOnChange: false)
                .Build();

            Assert.Equal("False", reloadedConfig["Media:GeoBypass"]);
            Assert.Equal("custom-xff", reloadedConfig["Media:Xff"]);
            Assert.Equal("True", reloadedConfig["Media:ForceIpv4"]);
        }
        finally
        {
            if (File.Exists(appSettingsPath))
            {
                File.Delete(appSettingsPath);
            }
        }
    }

    [Fact]
    public void ApplyRestrictionBypassArgs_WhenIncludeProxyFalse_OmitsProxyArgs()
    {
        var args = new List<string>();
        var settings = new DownloaderSettingsDto
        {
            Proxy = "http://gluetun:8888",
            GeoVerificationProxy = "http://proxy:8080"
        };

        YtDlpBypassHelper.ApplyRestrictionBypassArgs(args, settings, includeProxy: false);

        Assert.DoesNotContain("--proxy", args);
        Assert.DoesNotContain("http://gluetun:8888", args);
        Assert.DoesNotContain("--geo-verification-proxy", args);
        Assert.DoesNotContain("http://proxy:8080", args);
        Assert.Contains("--user-agent", args);
        Assert.Contains("--js-runtimes", args);
        Assert.Contains(args, a => a.Contains("deno"));
    }

    [Fact]
    public void ApplyRestrictionBypassArgs_WhenIncludeProxyTrue_IncludesConfiguredProxy()
    {
        var args = new List<string>();
        var settings = new DownloaderSettingsDto
        {
            Proxy = "http://gluetun:8888"
        };

        YtDlpBypassHelper.ApplyRestrictionBypassArgs(args, settings, includeProxy: true);

        Assert.Contains("--proxy", args);
        Assert.Contains("http://gluetun:8888", args);
    }
}


