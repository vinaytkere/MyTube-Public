using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MyTube.Backend.Application;
using MyTube.Backend.Domain;
using MyTube.Backend.Infrastructure;
using MyTube.Backend.Infrastructure.Ai;
using Xunit;
using Xunit.Abstractions;

namespace MyTube.Backend.Tests;

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

    public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _handler(request);
    }
}

public class MockMediaProcessor : IMediaProcessor
{
    public string ResolvePhysicalFilePath(string filePath) => filePath;

    public Task<VideoMetadata?> ExtractMetadataAsync(string filePath)
    {
        return Task.FromResult<VideoMetadata?>(new VideoMetadata { Duration = 120, Width = 1920, Height = 1080 });
    }

    public Task<bool> GenerateThumbnailAsync(string filePath, string destinationThumbnailPath, double offsetSeconds = 5)
    {
        return Task.FromResult(true);
    }

    public Task<List<string>> ExtractAudioChunksAsync(string filePath, string tempDir)
    {
        Directory.CreateDirectory(tempDir);
        var dummyFile = Path.Combine(tempDir, "chunk_001.mp3");
        File.WriteAllText(dummyFile, "dummy audio bytes");
        return Task.FromResult(new List<string> { dummyFile });
    }
}

public class AiOrchestratorTests
{
    private readonly ITestOutputHelper _output;

    public AiOrchestratorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private VideoDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<VideoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var db = new VideoDbContext(options);

        var defaultCategory = new Category { Id = 1, Name = "General", CreatedAt = DateTime.UtcNow, IsUserCreated = false };
        var techCategory = new Category { Id = 2, Name = "Technology", CreatedAt = DateTime.UtcNow, IsUserCreated = true };
        db.Categories.AddRange(defaultCategory, techCategory);

        db.AiModelQuotas.AddRange(
            new AiModelQuota { Id = 1, Provider = "Groq", ModelName = "whisper-large-v3-turbo", RpmLimit = 20, RpdLimit = 2000, TaskType = "STT", PriorityOrder = 1, WindowStartMinute = DateTime.UtcNow, WindowStartDay = DateTime.UtcNow.Date },
            new AiModelQuota { Id = 2, Provider = "Groq", ModelName = "groq/compound-mini", RpmLimit = 30, RpdLimit = 14400, TaskType = "Text", PriorityOrder = 1, WindowStartMinute = DateTime.UtcNow, WindowStartDay = DateTime.UtcNow.Date },
            new AiModelQuota { Id = 3, Provider = "Gemini", ModelName = "gemini-2.5-flash-lite", RpmLimit = 15, RpdLimit = 1000, TaskType = "Text", PriorityOrder = 2, WindowStartMinute = DateTime.UtcNow, WindowStartDay = DateTime.UtcNow.Date }
        );

        var video = new Video
        {
            Id = Guid.NewGuid(),
            CategoryId = 1,
            DisplayTitle = "Test Video",
            FileName = "test.mp4",
            FilePath = Path.Combine(Path.GetTempPath(), "test.mp4"),
            AiStatus = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        db.Videos.Add(video);
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task AnalyzeVideoAsync_WhenAllServicesRespond_ShouldCompleteSuccessfully()
    {
        var db = GetInMemoryDbContext();
        var videoId = await db.Videos.Select(v => v.Id).FirstAsync();

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "Groq:ApiKey", "mock-groq-key" },
            { "Gemini:ApiKey", "mock-gemini-key" }
        }).Build();

        var httpHandler = new MockHttpMessageHandler(async req =>
        {
            _output.WriteLine($"Mock HTTP request to: {req.RequestUri}");
            if (req.RequestUri!.AbsoluteUri.Contains("transcriptions"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"text\": \"This is a speech transcript of the video.\"}")
                };
            }

            if (req.RequestUri.AbsoluteUri.Contains("chat/completions"))
            {
                var json = "{\"choices\":[{\"message\":{\"content\":\"{\\\"suggestedTitle\\\":\\\"AI Summarized Video\\\",\\\"summary\\\":\\\"Great AI video content\\\",\\\"suggestedCategory\\\":\\\"Technology\\\",\\\"tags\\\":[\\\"tech\\\",\\\"ai\\\"],\\\"detectedLanguage\\\":\\\"English\\\"}\"}}]}";
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var httpClient = new HttpClient(httpHandler);
        var quotaManager = new AiQuotaManager(db, NullLogger<AiQuotaManager>.Instance);
        var secretService = new MyTube.Backend.Infrastructure.Security.SecretManagementService(null!, config, NullLogger<MyTube.Backend.Infrastructure.Security.SecretManagementService>.Instance);
        var aiService = new AiService(httpClient, quotaManager, config, secretService, NullLogger<AiService>.Instance);
        var mediaProcessor = new MockMediaProcessor();

        var orchestrator = new AiOrchestrator(
            db,
            mediaProcessor,
            aiService,
            NullLogger<AiOrchestrator>.Instance
        );

        var result = await orchestrator.AnalyzeVideoAsync(videoId);
        var updatedVideo = await db.Videos.Include(v => v.Category).FirstOrDefaultAsync(v => v.Id == videoId);

        _output.WriteLine($"Result: {result}, Status: {updatedVideo?.AiStatus}, Error: {updatedVideo?.ProcessingError}, Transcript: {updatedVideo?.Transcript}");

        Assert.True(result, $"AnalyzeVideoAsync failed. ProcessingError: {updatedVideo?.ProcessingError}");
        Assert.NotNull(updatedVideo);
        Assert.Equal("Completed", updatedVideo.AiStatus);
        Assert.Equal("AI Summarized Video", updatedVideo.DisplayTitle);
        Assert.Equal("Great AI video content", updatedVideo.Summary);
        Assert.Equal("Technology", updatedVideo.Category?.Name);
        Assert.Contains("This is a speech transcript", updatedVideo.Transcript);
    }
}
