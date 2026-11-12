using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MyTube.Backend.Application.Radarr;
using MyTube.Backend.Domain;
using MyTube.Backend.Infrastructure;
using MyTube.Backend.Infrastructure.Radarr;
using Xunit;

namespace MyTube.Backend.Tests;

public class RadarrServiceTests
{
    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    private class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler? _handler;
        private readonly HttpClient? _client;
        public FakeHttpClientFactory(HttpClient client) => _client = client;
        public FakeHttpClientFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => _handler != null ? new HttpClient(_handler, disposeHandler: false) : _client!;
    }

    private class FakeServiceScope : IServiceScope
    {
        public FakeServiceScope(IServiceProvider provider) => ServiceProvider = provider;
        public IServiceProvider ServiceProvider { get; }
        public void Dispose() { }
    }

    private class FakeScopeFactory : IServiceScopeFactory
    {
        private readonly IServiceProvider _provider;
        public FakeScopeFactory(IServiceProvider provider) => _provider = provider;
        public IServiceScope CreateScope() => new FakeServiceScope(_provider);
    }

    [Fact]
    public async Task TestConnectionAsync_WhenParametersEmpty_ReturnsNotConfigured()
    {
        var client = new HttpClient(new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var clientFactory = new FakeHttpClientFactory(client);
        var services = new ServiceCollection().BuildServiceProvider();
        var scopeFactory = new FakeScopeFactory(services);
        var configuration = new ConfigurationBuilder().Build();

        var service = new RadarrService(clientFactory, scopeFactory, configuration, NullLogger<RadarrService>.Instance);

        var result = await service.TestConnectionAsync("", "");
        Assert.False(result.IsConfigured);
        Assert.False(result.IsConnected);
        Assert.Contains("configured", result.ErrorMessage);
    }

    [Fact]
    public async Task TestConnectionAsync_WhenRadarrReturnsValidStatus_ReturnsConnected()
    {
        var statusJson = JsonSerializer.Serialize(new
        {
            appName = "Radarr",
            version = "5.2.6.8376",
            isProduction = true
        });

        var handler = new FakeHttpMessageHandler(req =>
        {
            if (req.RequestUri?.AbsolutePath.Contains("/api/v3/system/status") == true)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(statusJson)
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = new HttpClient(handler);
        var clientFactory = new FakeHttpClientFactory(client);
        var services = new ServiceCollection().BuildServiceProvider();
        var scopeFactory = new FakeScopeFactory(services);

        var inMemoryConfig = new Dictionary<string, string?>
        {
            { "Radarr:BaseUrl", "http://localhost:7878" },
            { "Radarr:ApiKey", "test-api-key" }
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

        var service = new RadarrService(clientFactory, scopeFactory, configuration, NullLogger<RadarrService>.Instance);

        var result = await service.GetStatusAsync();
        Assert.True(result.IsConfigured);
        Assert.True(result.IsConnected);
        Assert.Equal("Radarr", result.AppName);
        Assert.Equal("5.2.6.8376", result.Version);
    }

    [Fact]
    public async Task SearchMoviesAsync_CrossReferencesHomelabDatabase()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<VideoDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var videoId = Guid.NewGuid();
        using (var db = new VideoDbContext(options))
        {
            db.Videos.Add(new Video
            {
                Id = videoId,
                DisplayTitle = "Inception",
                FileName = "Inception.2010.1080p.mp4",
                FilePath = "/media/Inception.2010.1080p.mp4",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var services = new ServiceCollection();
        services.AddScoped(_ => new VideoDbContext(options));
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = new FakeScopeFactory(serviceProvider);

        var searchJson = JsonSerializer.Serialize(new[]
        {
            new
            {
                id = 12,
                title = "Inception",
                year = 2010,
                tmdbId = 27205,
                hasFile = false
            },
            new
            {
                id = 0,
                title = "Interstellar",
                year = 2014,
                tmdbId = 157336,
                hasFile = false
            }
        });

        var handler = new FakeHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(searchJson)
            };
        });

        var client = new HttpClient(handler);
        var clientFactory = new FakeHttpClientFactory(client);

        var inMemoryConfig = new Dictionary<string, string?>
        {
            { "Radarr:BaseUrl", "http://localhost:7878" },
            { "Radarr:ApiKey", "test-api-key" }
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

        var service = new RadarrService(clientFactory, scopeFactory, configuration, NullLogger<RadarrService>.Instance);

        var movies = await service.SearchMoviesAsync("Inception");

        Assert.Equal(2, movies.Count);

        // Inception should match local homelab video
        var inception = movies.Find(m => m.Title == "Inception");
        Assert.NotNull(inception);
        Assert.True(inception.IsAvailableInHomelab);
        Assert.Equal(videoId, inception.LocalVideoId);

        // Interstellar should not be in homelab
        var interstellar = movies.Find(m => m.Title == "Interstellar");
        Assert.NotNull(interstellar);
        Assert.False(interstellar.IsAvailableInHomelab);
        Assert.Null(interstellar.LocalVideoId);
    }

    [Fact]
    public async Task GetFileDownloadAsync_ResolvesMimeTypesCorrectly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "mytube_test_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            var pdfFile = Path.Combine(tempDir, "document.pdf");
            var videoFile = Path.Combine(tempDir, "movie.mkv");
            var imageFile = Path.Combine(tempDir, "poster.jpg");
            var textFile = Path.Combine(tempDir, "info.nfo");

            await System.IO.File.WriteAllTextAsync(pdfFile, "%PDF-1.4 test");
            await System.IO.File.WriteAllTextAsync(videoFile, "fake video content");
            await System.IO.File.WriteAllTextAsync(imageFile, "fake image content");
            await System.IO.File.WriteAllTextAsync(textFile, "release info");

            var clientFactory = new FakeHttpClientFactory(new HttpClient());
            var services = new ServiceCollection().BuildServiceProvider();
            var scopeFactory = new FakeScopeFactory(services);

            var inMemoryConfig = new Dictionary<string, string?>
            {
                { "Media:RootPath", tempDir },
                { "Radarr:BaseUrl", "http://localhost:7878" }
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

            var service = new RadarrService(clientFactory, scopeFactory, configuration, NullLogger<RadarrService>.Instance);

            // 1. PDF Download
            var pdfDownload = await service.GetFileDownloadAsync(pdfFile);
            Assert.NotNull(pdfDownload);
            Assert.Equal("application/pdf", pdfDownload.Value.ContentType);
            Assert.Equal("document.pdf", pdfDownload.Value.FileName);

            // 2. Video Download
            var videoDownload = await service.GetFileDownloadAsync(videoFile);
            Assert.NotNull(videoDownload);
            Assert.Equal("video/x-matroska", videoDownload.Value.ContentType);
            Assert.Equal("movie.mkv", videoDownload.Value.FileName);

            // 3. Image Download
            var imgDownload = await service.GetFileDownloadAsync(imageFile);
            Assert.NotNull(imgDownload);
            Assert.Equal("image/jpeg", imgDownload.Value.ContentType);
            Assert.Equal("poster.jpg", imgDownload.Value.FileName);

            // 4. Text/NFO Download
            var txtDownload = await service.GetFileDownloadAsync(textFile);
            Assert.NotNull(txtDownload);
            Assert.Equal("text/plain", txtDownload.Value.ContentType);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task GetDownloadedMediaSummaryAsync_CalculatesTotalsAndSetsNonDeletableFolder()
    {
        var libraryJson = JsonSerializer.Serialize(new[]
        {
            new
            {
                id = 1,
                title = "Inception",
                year = 2010,
                hasFile = true,
                sizeOnDisk = 2147483648L
            },
            new
            {
                id = 2,
                title = "Interstellar",
                year = 2014,
                hasFile = false,
                sizeOnDisk = 0L
            }
        });

        var handler = new FakeHttpMessageHandler(req =>
        {
            if (req.RequestUri?.AbsolutePath.EndsWith("/api/v3/movie") == true)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(libraryJson)
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = new HttpClient(handler);
        var clientFactory = new FakeHttpClientFactory(client);
        var services = new ServiceCollection().BuildServiceProvider();
        var scopeFactory = new FakeScopeFactory(services);

        var inMemoryConfig = new Dictionary<string, string?>
        {
            { "Media:RootPath", "/srv/mytube/videos" },
            { "Radarr:BaseUrl", "http://localhost:7878" }
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

        var service = new RadarrService(clientFactory, scopeFactory, configuration, NullLogger<RadarrService>.Instance);

        var summary = await service.GetDownloadedMediaSummaryAsync();
        Assert.Equal(1, summary.TotalDownloadedCount);
        Assert.Equal(2147483648L, summary.TotalSizeBytes);
        Assert.Equal("Radarr", summary.ProtectedFolderName);
        Assert.Single(summary.DownloadedMovies);
    }

    [Fact]
    public async Task AutoGrabMovieAsync_WhenApprovedReleaseFound_GrabsReleaseSuccessfully()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/api/v3/rootfolder"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[{\"id\":1,\"path\":\"/movies\"}]") };
            }
            if (path.EndsWith("/api/v3/qualityprofile"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[{\"id\":1,\"name\":\"Any\"}]") };
            }
            if (path.EndsWith("/api/v3/movie/lookup"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[{\"tmdbId\":100,\"title\":\"Inception\",\"year\":2010}]") };
            }
            if (path.EndsWith("/api/v3/movie") && request.Method == HttpMethod.Post)
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":42,\"tmdbId\":100,\"title\":\"Inception\",\"year\":2010}") };
            }
            if (path.EndsWith("/api/v3/release") && request.Method == HttpMethod.Get)
            {
                var releases = new[]
                {
                    new { guid = "g1", title = "Inception.1080p", size = 2000000000L, indexer = "TPB", indexerId = 1, seeders = 50, approved = true, downloadAllowed = true, quality = new { quality = new { name = "1080p" } } }
                };
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(releases)) };
            }
            if (path.EndsWith("/api/v3/release") && request.Method == HttpMethod.Post)
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"success\":true}") };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var clientFactory = new FakeHttpClientFactory(handler);

        var options = new DbContextOptionsBuilder<VideoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var services = new ServiceCollection();
        services.AddSingleton(_ => new VideoDbContext(options));
        var scopeFactory = new FakeScopeFactory(services.BuildServiceProvider());

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "Radarr:BaseUrl", "http://localhost:7878" }
        }).Build();

        var service = new RadarrService(clientFactory, scopeFactory, configuration, NullLogger<RadarrService>.Instance);

        var result = await service.AutoGrabMovieAsync(new RadarrAddMovieRequest
        {
            TmdbId = 100,
            Title = "Inception",
            Year = 2010
        });

        Assert.True(result.Success, $"AutoGrab failed: {result.Message}");
        Assert.True(result.Grabbed);
        Assert.Equal("Inception.1080p", result.ReleaseTitle);
        Assert.Equal("Radarr", result.Source);
        Assert.Equal(50, result.Seeders);
    }
}

