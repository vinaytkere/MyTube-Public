using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MyTube.Backend.Application;
using MyTube.Backend.Application.Features.Categories.Commands;
using MyTube.Backend.Application.Features.Categories.Queries;
using MyTube.Backend.Domain;
using MyTube.Backend.Infrastructure;
using MyTube.Backend.Infrastructure.Repositories;
using Xunit;

namespace MyTube.Backend.Tests;

public class CqrsCategoryTests
{
    private class FakeVideoService : IVideoService
    {
        public bool DeleteCategoryCalled { get; private set; }
        public int LastDeletedId { get; private set; }

        public Task<bool> DeleteCategoryAsync(int categoryId, CancellationToken cancellationToken = default)
        {
            DeleteCategoryCalled = true;
            LastDeletedId = categoryId;
            return Task.FromResult(true);
        }

        public Task<PagedResult<VideoDto>> GetVideosAsync(string userId, string baseUrl, string? search, int? categoryId, string? sortBy, string? mediaType, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<VideoDto?> GetVideoByIdAsync(Guid id, string userId, string baseUrl, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<VideoDto>> GetFavoritesAsync(string userId, string baseUrl, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> ToggleFavoriteAsync(string userId, Guid videoId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateProgressAsync(string userId, Guid videoId, double positionSeconds, double durationSeconds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<WatchProgressDto?> GetProgressAsync(string userId, Guid videoId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<VideoDto>> GetContinueWatchingAsync(string userId, string baseUrl, int limit = 6, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CategoryDto> CreateCategoryAsync(string name, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> UpdateVideoCategoryAsync(Guid videoId, int categoryId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> UpdateVideosCategoryAsync(IEnumerable<Guid> videoIds, int categoryId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> DeleteVideosAsync(IEnumerable<Guid> videoIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> RenameVideoAsync(Guid videoId, string newTitle, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private VideoDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<VideoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new VideoDbContext(options);
    }

    [Fact]
    public void CreateCategoryCommandValidator_ShouldValidateName()
    {
        var validator = new CreateCategoryCommandValidator();

        var empty = validator.Validate(new CreateCategoryCommand(""));
        var tooLong = validator.Validate(new CreateCategoryCommand(new string('a', 101)));
        var valid = validator.Validate(new CreateCategoryCommand("Technology"));

        empty.IsValid.Should().BeFalse();
        tooLong.IsValid.Should().BeFalse();
        valid.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CreateCategoryCommandHandler_ShouldCreateOrReturnExisting()
    {
        using var db = CreateInMemoryDbContext();
        var repo = new CategoryRepository(db);
        var handler = new CreateCategoryCommandHandler(repo);

        // 1. First creation
        var cat1 = await handler.Handle(new CreateCategoryCommand("Gaming"), CancellationToken.None);
        cat1.Id.Should().BeGreaterThan(0);
        cat1.Name.Should().Be("Gaming");

        // 2. Second creation with same name (case-insensitive) should return existing
        var cat2 = await handler.Handle(new CreateCategoryCommand("gaming"), CancellationToken.None);
        cat2.Id.Should().Be(cat1.Id);
        cat2.Name.Should().Be("Gaming");
    }

    [Fact]
    public async Task GetCategoriesQueryHandler_ShouldReturnAllCategories()
    {
        using var db = CreateInMemoryDbContext();
        var repo = new CategoryRepository(db);

        await repo.AddAsync(new Category { Name = "Science", CreatedAt = DateTime.UtcNow });
        await repo.AddAsync(new Category { Name = "Art", CreatedAt = DateTime.UtcNow });
        await repo.SaveChangesAsync();

        var handler = new GetCategoriesQueryHandler(repo);
        var result = await handler.Handle(new GetCategoriesQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(c => c.Name).Should().Contain(new[] { "Art", "Science" });
    }

    [Fact]
    public async Task DeleteCategoryCommandHandler_ShouldDelegateToVideoService()
    {
        var videoService = new FakeVideoService();
        var handler = new DeleteCategoryCommandHandler(videoService);

        var result = await handler.Handle(new DeleteCategoryCommand(42), CancellationToken.None);

        result.Should().BeTrue();
        videoService.DeleteCategoryCalled.Should().BeTrue();
        videoService.LastDeletedId.Should().Be(42);
    }
}
