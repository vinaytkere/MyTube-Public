using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MyTube.Backend.Application;
using MyTube.Backend.Application.AI;
using MyTube.Backend.Application.Features.Videos.Commands;
using MyTube.Backend.Application.Features.Videos.Queries;
using MyTube.Backend.Domain;
using MyTube.Backend.Infrastructure;
using MyTube.Backend.Infrastructure.Repositories;
using Xunit;

namespace MyTube.Backend.Tests;

public class CqrsVideoTests
{
    private class FakeFirecrawlService : IFirecrawlService
    {
        public Task<string> SearchWebContextAsync(string query) =>
            Task.FromResult("Web context information about the topic");
    }

    private class FakeAiOrchestrator : IAiOrchestrator
    {
        public Task<string> GenerateQaAnswerAsync(string videoTitle, string question, string webContext, string videoSummary, string videoTranscript) =>
            Task.FromResult($"AI Answer for '{question}' regarding '{videoTitle}'.");

        public Task<bool> AnalyzeVideoAsync(Guid videoId) => Task.FromResult(true);
        public Task<int> BatchAnalyzeVideosAsync(IEnumerable<Guid> videoIds) => Task.FromResult(1);
    }

    private VideoDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<VideoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new VideoDbContext(options);
    }

    [Fact]
    public void GetVideosQueryValidator_ShouldValidatePaging()
    {
        var validator = new GetVideosQueryValidator();

        var invalidPage = validator.Validate(new GetVideosQuery("u", "b", null, null, null, null, 0, 20));
        var invalidPageSize = validator.Validate(new GetVideosQuery("u", "b", null, null, null, null, 1, 101));
        var valid = validator.Validate(new GetVideosQuery("u", "b", null, null, null, null, 1, 25));

        invalidPage.IsValid.Should().BeFalse();
        invalidPageSize.IsValid.Should().BeFalse();
        valid.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GetVideoByIdQueryValidator_ShouldValidateGuid()
    {
        var validator = new GetVideoByIdQueryValidator();

        var invalid = validator.Validate(new GetVideoByIdQuery(Guid.Empty, "u", "b"));
        var valid = validator.Validate(new GetVideoByIdQuery(Guid.NewGuid(), "u", "b"));

        invalid.IsValid.Should().BeFalse();
        valid.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleFavoriteCommandHandler_ShouldToggleViaRepository()
    {
        using var db = CreateInMemoryDbContext();
        var repo = new VideoRepository(db);
        var handler = new ToggleFavoriteCommandHandler(repo);
        var videoId = Guid.NewGuid();

        var res1 = await handler.Handle(new ToggleFavoriteCommand("user1", videoId), CancellationToken.None);
        res1.Should().BeTrue();

        var res2 = await handler.Handle(new ToggleFavoriteCommand("user1", videoId), CancellationToken.None);
        res2.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateProgressCommandHandler_And_GetProgressQueryHandler_ShouldPersistProgress()
    {
        using var db = CreateInMemoryDbContext();
        var repo = new VideoRepository(db);
        var videoId = Guid.NewGuid();

        var updateHandler = new UpdateProgressCommandHandler(repo);
        await updateHandler.Handle(new UpdateProgressCommand("user1", videoId, 45, 120), CancellationToken.None);

        var getHandler = new GetProgressQueryHandler(repo);
        var progress = await getHandler.Handle(new GetProgressQuery("user1", videoId), CancellationToken.None);

        progress.Should().NotBeNull();
        progress!.PositionSeconds.Should().Be(45);
        progress.DurationSeconds.Should().Be(120);
        progress.Completed.Should().BeFalse();
    }

    [Fact]
    public async Task AskVideoQuestionCommandHandler_ShouldOrchestrateAiAndPersistQa()
    {
        using var db = CreateInMemoryDbContext();
        var repo = new VideoRepository(db);
        var firecrawl = new FakeFirecrawlService();
        var ai = new FakeAiOrchestrator();

        var category = new Category { Name = "Education", CreatedAt = DateTime.UtcNow };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var video = new Video
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            DisplayTitle = "Machine Learning Course",
            FileName = "ml.mp4",
            FilePath = "/media/ml.mp4",
            Summary = "Comprehensive intro to neural nets",
            ScanStatus = ScanState.Ready,
            CreatedAt = DateTime.UtcNow
        };
        await repo.AddAsync(video);
        await repo.SaveChangesAsync();

        var handler = new AskVideoQuestionCommandHandler(repo, firecrawl, ai);

        var command = new AskVideoQuestionCommand(video.Id, "testuser", "What is gradient descent?");
        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.VideoId.Should().Be(video.Id);
        result.Question.Should().Be("What is gradient descent?");
        result.Answer.Should().Contain("Machine Learning Course");

        // Verify stored in Q&A history
        var historyHandler = new GetVideoQaHistoryQueryHandler(repo);
        var history = await historyHandler.Handle(new GetVideoQaHistoryQuery(video.Id), CancellationToken.None);

        history.Should().HaveCount(1);
        history[0].Answer.Should().Be(result.Answer);
    }
}
