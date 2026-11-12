using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MyTube.Backend.Infrastructure;
using MyTube.Backend.Infrastructure.Ai;
using MyTube.Backend.Infrastructure.Security;
using Xunit;

namespace MyTube.Backend.Tests;

public class AiServiceTests
{
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request);
    }

    private static VideoDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<VideoDbContext>()
            .UseInMemoryDatabase(databaseName: $"AiServiceTestDb_{Guid.NewGuid()}")
            .Options;
        var db = new VideoDbContext(options);
        db.AiModelQuotas.AddRange(
            new Domain.AiModelQuota { Id = 1, Provider = "Groq", ModelName = "llama-3.3-70b-versatile", RpmLimit = 30, RpdLimit = 14400, TaskType = "Text", PriorityOrder = 1, WindowStartMinute = DateTime.UtcNow, WindowStartDay = DateTime.UtcNow.Date },
            new Domain.AiModelQuota { Id = 2, Provider = "DeepSeek", ModelName = "deepseek-chat", RpmLimit = 30, RpdLimit = 10000, TaskType = "Text", PriorityOrder = 1, WindowStartMinute = DateTime.UtcNow, WindowStartDay = DateTime.UtcNow.Date },
            new Domain.AiModelQuota { Id = 3, Provider = "Gemini", ModelName = "gemini-2.5-flash", RpmLimit = 15, RpdLimit = 1000, TaskType = "Text", PriorityOrder = 1, WindowStartMinute = DateTime.UtcNow, WindowStartDay = DateTime.UtcNow.Date },
            new Domain.AiModelQuota { Id = 4, Provider = "Cerebras", ModelName = "llama3.1-70b", RpmLimit = 30, RpdLimit = 14400, TaskType = "Text", PriorityOrder = 1, WindowStartMinute = DateTime.UtcNow, WindowStartDay = DateTime.UtcNow.Date }
        );
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task CompleteAsync_WithGroq_DispatchesToOpenAiEndpointAndParsesContent()
    {
        // Arrange
        var db = GetInMemoryDbContext();
        var quotaManager = new AiQuotaManager(db, NullLogger<AiQuotaManager>.Instance);

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "Groq:ApiKey", "gsk-mock-key" }
        }).Build();

        string? capturedAuth = null;
        string? capturedUri = null;

        var handler = new MockHttpMessageHandler(req =>
        {
            capturedAuth = req.Headers.Authorization?.Parameter;
            capturedUri = req.RequestUri?.AbsoluteUri;
            var json = "{\"choices\":[{\"message\":{\"content\":\"Groq response text\"}}]}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        });

        var httpClient = new HttpClient(handler);
        var secretService = new SecretManagementService(null!, config, NullLogger<SecretManagementService>.Instance);
        var aiService = new AiService(httpClient, quotaManager, config, secretService, NullLogger<AiService>.Instance);

        // Act
        var result = await aiService.CompleteAsync("Groq", "System", "User");

        // Assert
        result.Should().Be("Groq response text");
        capturedAuth.Should().Be("gsk-mock-key");
        capturedUri.Should().Contain("api.groq.com");
    }

    [Fact]
    public async Task CompleteAsync_WithDeepSeek_DispatchesToDeepSeekEndpointAndParsesContent()
    {
        // Arrange
        var db = GetInMemoryDbContext();
        var quotaManager = new AiQuotaManager(db, NullLogger<AiQuotaManager>.Instance);

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "DeepSeek:ApiKey", "sk-deepseek-mock" }
        }).Build();

        string? capturedUri = null;

        var handler = new MockHttpMessageHandler(req =>
        {
            capturedUri = req.RequestUri?.AbsoluteUri;
            var json = "{\"choices\":[{\"message\":{\"content\":\"DeepSeek response text\"}}]}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        });

        var httpClient = new HttpClient(handler);
        var secretService = new SecretManagementService(null!, config, NullLogger<SecretManagementService>.Instance);
        var aiService = new AiService(httpClient, quotaManager, config, secretService, NullLogger<AiService>.Instance);

        // Act
        var result = await aiService.CompleteAsync("DeepSeek", "System", "User");

        // Assert
        result.Should().Be("DeepSeek response text");
        capturedUri.Should().Contain("api.deepseek.com");
    }

    [Fact]
    public async Task CompleteAsync_WithGemini_DispatchesToGeminiEndpointAndParsesContent()
    {
        // Arrange
        var db = GetInMemoryDbContext();
        var quotaManager = new AiQuotaManager(db, NullLogger<AiQuotaManager>.Instance);

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "Gemini:ApiKey", "AIzaSyMockKey" }
        }).Build();

        string? capturedUri = null;

        var handler = new MockHttpMessageHandler(req =>
        {
            capturedUri = req.RequestUri?.AbsoluteUri;
            var json = "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Gemini candidate text\"}]}}]}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        });

        var httpClient = new HttpClient(handler);
        var secretService = new SecretManagementService(null!, config, NullLogger<SecretManagementService>.Instance);
        var aiService = new AiService(httpClient, quotaManager, config, secretService, NullLogger<AiService>.Instance);

        // Act
        var result = await aiService.CompleteAsync("Gemini", "System", "User");

        // Assert
        result.Should().Be("Gemini candidate text");
        capturedUri.Should().Contain("generativelanguage.googleapis.com");
        capturedUri.Should().Contain("AIzaSyMockKey");
    }

    [Fact]
    public async Task AnalyzeTextAsync_DeserializesMetadataResultCorrectly()
    {
        // Arrange
        var db = GetInMemoryDbContext();
        var quotaManager = new AiQuotaManager(db, NullLogger<AiQuotaManager>.Instance);

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "Groq:ApiKey", "gsk-mock-key" }
        }).Build();

        var handler = new MockHttpMessageHandler(req =>
        {
            var json = """
            {
                "choices": [
                    {
                        "message": {
                            "content": "{\"suggestedTitle\":\"Clean Code in C#\",\"summary\":\"A comprehensive guide to SOLID and DRY\",\"suggestedCategory\":\"Programming\",\"tags\":[\"csharp\",\"architecture\"],\"detectedLanguage\":\"en\"}"
                        }
                    }
                ]
            }
            """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        });

        var httpClient = new HttpClient(handler);
        var secretService = new SecretManagementService(null!, config, NullLogger<SecretManagementService>.Instance);
        var aiService = new AiService(httpClient, quotaManager, config, secretService, NullLogger<AiService>.Instance);

        // Act
        var analysis = await aiService.AnalyzeTextAsync("clean_code.mp4", "Transcript content", "Programming, Science");

        // Assert
        analysis.Should().NotBeNull();
        analysis!.SuggestedTitle.Should().Be("Clean Code in C#");
        analysis.Summary.Should().Be("A comprehensive guide to SOLID and DRY");
        analysis.SuggestedCategory.Should().Be("Programming");
        analysis.Tags.Should().Contain(new[] { "csharp", "architecture" });
        analysis.DetectedLanguage.Should().Be("en");
    }

    [Fact]
    public async Task CompleteAsync_WithCerebras_DispatchesToCerebrasEndpointAndParsesContent()
    {
        // Arrange
        var db = GetInMemoryDbContext();
        var quotaManager = new AiQuotaManager(db, NullLogger<AiQuotaManager>.Instance);

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "Cerebras:ApiKey", "csk-mock-key" }
        }).Build();

        string? capturedAuth = null;
        string? capturedUri = null;

        var handler = new MockHttpMessageHandler(req =>
        {
            capturedAuth = req.Headers.Authorization?.Parameter;
            capturedUri = req.RequestUri?.AbsoluteUri;
            var json = "{\"choices\":[{\"message\":{\"content\":\"Ultra-fast Cerebras response\"}}]}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        });

        var httpClient = new HttpClient(handler);
        var secretService = new SecretManagementService(null!, config, NullLogger<SecretManagementService>.Instance);
        var aiService = new AiService(httpClient, quotaManager, config, secretService, NullLogger<AiService>.Instance);

        // Act
        var result = await aiService.CompleteAsync("Cerebras", "System", "User");

        // Assert
        result.Should().Be("Ultra-fast Cerebras response");
        capturedAuth.Should().Be("csk-mock-key");
        capturedUri.Should().Contain("api.cerebras.ai");
    }

    [Fact]
    public async Task TranscribeAudioAsync_WithGroqWhisper_ReturnsTranscription()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "dummy audio bytes");

            var db = GetInMemoryDbContext();
            var quotaManager = new AiQuotaManager(db, NullLogger<AiQuotaManager>.Instance);

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Groq:ApiKey", "gsk-mock-key" }
            }).Build();

            var handler = new MockHttpMessageHandler(req =>
            {
                var json = "{\"text\": \"Transcribed audio via Groq Whisper\"}";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
            });

            var httpClient = new HttpClient(handler);
            var secretService = new SecretManagementService(null!, config, NullLogger<SecretManagementService>.Instance);
            var aiService = new AiService(httpClient, quotaManager, config, secretService, NullLogger<AiService>.Instance);

            // Act
            var result = await aiService.TranscribeAudioAsync(tempFile);

            // Assert
            result.Should().Be("Transcribed audio via Groq Whisper");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
