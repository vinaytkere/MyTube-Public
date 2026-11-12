using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MyTube.Backend.Domain;
using MyTube.Backend.Infrastructure;
using MyTube.Backend.Infrastructure.Repositories;
using Xunit;

namespace MyTube.Backend.Tests;

public class RepositoryTests
{
    private VideoDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<VideoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new VideoDbContext(options);
    }

    [Fact]
    public async Task CategoryRepository_AddAndGetByName_ShouldBeCaseInsensitive()
    {
        using var db = CreateInMemoryDbContext();
        var repo = new CategoryRepository(db);

        var category = new Category { Name = "Podcasts", CreatedAt = DateTime.UtcNow };
        await repo.AddAsync(category);
        await repo.SaveChangesAsync();

        var result = await repo.GetByNameAsync("podcasts");

        result.Should().NotBeNull();
        result!.Name.Should().Be("Podcasts");
        result.Id.Should().Be(category.Id);
    }

    [Fact]
    public async Task CategoryRepository_GetAllWithCount_ShouldIncludeVideos()
    {
        using var db = CreateInMemoryDbContext();
        var category = new Category { Name = "Music", CreatedAt = DateTime.UtcNow };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        db.Videos.Add(new Video
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            FileName = "song.mp3",
            FilePath = "/media/song.mp3",
            DisplayTitle = "Song",
            ScanStatus = ScanState.Ready,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var repo = new CategoryRepository(db);
        var categories = await repo.GetAllWithCountAsync();

        categories.Should().HaveCount(1);
        categories[0].Videos.Should().HaveCount(1);
    }

    [Fact]
    public async Task UserRepository_ExistsByUsernameOrEmail_ShouldDetectDuplicates()
    {
        using var db = CreateInMemoryDbContext();
        var repo = new UserRepository(db);

        var user = new User
        {
            Username = "testuser",
            Email = "testuser@test.com",
            PasswordHash = "hash123",
            FullName = "Test User",
            Role = "Administrator",
            CreatedAt = DateTime.UtcNow
        };
        await repo.AddAsync(user);
        await repo.SaveChangesAsync();

        var existsUsername = await repo.ExistsByUsernameOrEmailAsync("TESTUSER", "other@test.com");
        var existsEmail = await repo.ExistsByUsernameOrEmailAsync("other", "TESTUSER@TEST.COM");
        var existsNone = await repo.ExistsByUsernameOrEmailAsync("different", "different@test.com");

        existsUsername.Should().BeTrue();
        existsEmail.Should().BeTrue();
        existsNone.Should().BeFalse();
    }

    [Fact]
    public async Task UserRepository_OtpWorkflow_ShouldSaveAndInvalidate()
    {
        using var db = CreateInMemoryDbContext();
        var repo = new UserRepository(db);

        var otp = new OtpRecord
        {
            Email = "user@test.com",
            Code = "123456",
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        await repo.SaveOtpAsync(otp);

        var retrieved = await repo.GetLatestValidOtpAsync("user@test.com", "123456");
        retrieved.Should().NotBeNull();
        retrieved!.Code.Should().Be("123456");

        await repo.InvalidateOtpsAsync("user@test.com");

        var afterInvalidation = await repo.GetLatestValidOtpAsync("user@test.com", "123456");
        afterInvalidation.Should().BeNull();
    }

    [Fact]
    public async Task VideoRepository_GetPagedVideos_ShouldFilterAndSort()
    {
        using var db = CreateInMemoryDbContext();
        var category = new Category { Name = "Tutorials", CreatedAt = DateTime.UtcNow };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var v1 = new Video
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            FileName = "csharp_basics.mp4",
            FilePath = "/media/csharp_basics.mp4",
            DisplayTitle = "C# Basics",
            Duration = 100,
            ScanStatus = ScanState.Ready,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        var v2 = new Video
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            FileName = "audio_podcast.mp3",
            FilePath = "/media/audio_podcast.mp3",
            DisplayTitle = "Audio Podcast",
            Duration = 200,
            ScanStatus = ScanState.Ready,
            CreatedAt = DateTime.UtcNow
        };

        db.Videos.AddRange(v1, v2);
        await db.SaveChangesAsync();

        var repo = new VideoRepository(db);

        // Filter by search
        var (searchVideos, searchCount) = await repo.GetPagedVideosAsync("basics", null, null, null, 1, 10);
        searchCount.Should().Be(1);
        searchVideos.Should().ContainSingle(v => v.DisplayTitle == "C# Basics");

        // Filter by mediaType "audio"
        var (audioVideos, audioCount) = await repo.GetPagedVideosAsync(null, null, null, "audio", 1, 10);
        audioCount.Should().Be(1);
        audioVideos[0].FileName.Should().Be("audio_podcast.mp3");

        // Sort by duration descending
        var (sortedVideos, _) = await repo.GetPagedVideosAsync(null, null, "duration", null, 1, 10);
        sortedVideos.First().Duration.Should().Be(200);
    }

    [Fact]
    public async Task VideoRepository_ToggleFavorite_ShouldToggleCorrectly()
    {
        using var db = CreateInMemoryDbContext();
        var repo = new VideoRepository(db);
        var videoId = Guid.NewGuid();
        var userId = "testuser";

        // First toggle -> favorite added (returns true)
        var isFav1 = await repo.ToggleFavoriteAsync(userId, videoId);
        isFav1.Should().BeTrue();

        var favs = await repo.GetFavoriteIdsAsync(userId, new[] { videoId });
        favs.Should().Contain(videoId);

        // Second toggle -> favorite removed (returns false)
        var isFav2 = await repo.ToggleFavoriteAsync(userId, videoId);
        isFav2.Should().BeFalse();

        var favsAfter = await repo.GetFavoriteIdsAsync(userId, new[] { videoId });
        favsAfter.Should().NotContain(videoId);
    }

    [Fact]
    public async Task VideoRepository_SaveUserProgress_ShouldRecordAndCompleteAt95Percent()
    {
        using var db = CreateInMemoryDbContext();
        var repo = new VideoRepository(db);
        var videoId = Guid.NewGuid();
        var userId = "testuser";

        // 50% watched -> not completed
        await repo.SaveUserProgressAsync(userId, videoId, 50, 100);
        var p1 = await repo.GetUserProgressAsync(userId, videoId);
        p1.Should().NotBeNull();
        p1!.Completed.Should().BeFalse();
        p1.PositionSeconds.Should().Be(50);

        // 96% watched -> completed
        await repo.SaveUserProgressAsync(userId, videoId, 96, 100);
        var p2 = await repo.GetUserProgressAsync(userId, videoId);
        p2!.Completed.Should().BeTrue();
    }
}
