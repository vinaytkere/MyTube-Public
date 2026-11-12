using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyTube.Backend.Domain;
using MyTube.Backend.Infrastructure;
using Xunit;

namespace MyTube.Backend.Tests;

public class VideoQaTests
{
    [Fact]
    public async Task CanAddAndRetrieveVideoQa()
    {
        var options = new DbContextOptionsBuilder<VideoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var db = new VideoDbContext(options);

        var video = new Video
        {
            Id = Guid.NewGuid(),
            FilePath = "/tmp/test.mp4",
            FileName = "test.mp4",
            DisplayTitle = "Test Video",
            CreatedAt = DateTime.UtcNow
        };
        db.Videos.Add(video);
        await db.SaveChangesAsync();

        var qa = new VideoQa
        {
            VideoId = video.Id,
            UserId = "testuser",
            Question = "What is this test?",
            Answer = "This is a test answer.",
            FirecrawlContext = "Test context",
            CreatedAt = DateTime.UtcNow
        };

        db.VideoQas.Add(qa);
        await db.SaveChangesAsync();

        var saved = await db.VideoQas.FirstOrDefaultAsync(q => q.VideoId == video.Id);
        Assert.NotNull(saved);
        Assert.Equal("What is this test?", saved.Question);
        Assert.Equal("This is a test answer.", saved.Answer);
    }
}
