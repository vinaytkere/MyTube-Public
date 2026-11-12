using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MyTube.Backend.Application.AI;
using MyTube.Backend.Domain;
using MyTube.Backend.Infrastructure;
using MyTube.Backend.Infrastructure.FileNaming;
using Xunit;

namespace MyTube.Backend.Tests;

public class MediaOrganizerServiceTests : IDisposable
{
    private readonly string _tempMediaDir;
    private readonly VideoDbContext _db;

    public MediaOrganizerServiceTests()
    {
        _tempMediaDir = Path.Combine(Path.GetTempPath(), "MyTube_Test_Media_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempMediaDir);

        var options = new DbContextOptionsBuilder<VideoDbContext>()
            .UseInMemoryDatabase("MediaOrganizerTestDb_" + Guid.NewGuid().ToString("N"))
            .Options;

        _db = new VideoDbContext(options);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempMediaDir))
            {
                Directory.Delete(_tempMediaDir, true);
            }
        }
        catch { }

        _db.Dispose();
    }

    private class FakeSmartRouteClient : ISmartRouteClient
    {
        public MediaClassificationResult? ResultToReturn { get; set; }

        public Task<MediaClassificationResult?> ClassifyAndSummarizeMediaAsync(
            string title, string? uploader, string? description, string? subtitleExcerpt, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ResultToReturn);
        }

        public Task<MediaClassificationResult?> ClassifyMediaWithEvidenceAsync(
            string title, string? uploader, string? description, string? subtitleExcerpt, string? mediaEvidence, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ResultToReturn);
        }
    }

    [Fact]
    public async Task OrganizeMediaAsync_WhenMovie_RenamesToLanguageTitleYearAndMovesFileAndGeneratesNfo()
    {
        // Arrange
        var fakeClient = new FakeSmartRouteClient
        {
            ResultToReturn = new MediaClassificationResult
            {
                MediaType = "Video",
                Category = "Movies",
                Language = "English",
                CanonicalTitle = "Inception",
                ReleaseYear = 2010,
                PrimaryGenre = "Sci-Fi",
                ArtistOrDirector = "Christopher Nolan",
                Summary = "A thief who steals corporate secrets through dream-sharing technology is tasked with an inception.",
                SuggestedFileName = "English-Inception-2010"
            }
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Media:RootPath", _tempMediaDir },
                { "Media:Organizer:AutoOrganizeOnDownloadComplete", "true" },
                { "Media:Organizer:GenerateNfoFile", "true" },
                { "Media:Organizer:MoveSubtitles", "true" }
            })
            .Build();

        var organizer = new MediaOrganizerService(_db, fakeClient, config, NullLogger<MediaOrganizerService>.Instance);

        // Create dummy source video & companion subtitle file
        var sourceSubDir = Path.Combine(_tempMediaDir, "Downloads");
        Directory.CreateDirectory(sourceSubDir);
        var sourceFilePath = Path.Combine(sourceSubDir, "raw_inception_trailer_1080p.mp4");
        await File.WriteAllTextAsync(sourceFilePath, "fake video data");

        var sourceSubtitlePath = Path.Combine(sourceSubDir, "raw_inception_trailer_1080p.en.srt");
        await File.WriteAllTextAsync(sourceSubtitlePath, "1\n00:00:01,000 --> 00:00:04,000\nHello World");

        var category = new Category { Name = "Downloads", CreatedAt = DateTime.UtcNow };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        var video = new Video
        {
            DisplayTitle = "Inception (2010) Official Trailer #1",
            FileName = "raw_inception_trailer_1080p.mp4",
            FilePath = sourceFilePath,
            Height = 1080,
            CategoryId = category.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Videos.Add(video);
        await _db.SaveChangesAsync();

        // Act
        var result = await organizer.OrganizeMediaAsync(video.Id);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("English-Inception-2010.mp4", result.NewFileName);
        Assert.True(File.Exists(result.NewPath), "Target organized movie file should exist.");
        Assert.Contains("Videos" + Path.DirectorySeparatorChar + "Movies", result.NewPath);
        Assert.False(File.Exists(sourceFilePath), "Old source video file should have been moved.");

        // Check NFO file
        var targetDir = Path.GetDirectoryName(result.NewPath)!;
        var nfoPath = Path.Combine(targetDir, "English-Inception-2010.nfo");
        Assert.True(File.Exists(nfoPath), "Plex/Kodi .nfo file should have been created.");
        var nfoContent = await File.ReadAllTextAsync(nfoPath);
        Assert.Contains("<title>Inception</title>", nfoContent);
        Assert.Contains("<language>English</language>", nfoContent);
        Assert.Contains("<year>2010</year>", nfoContent);
        Assert.Contains("<director>Christopher Nolan</director>", nfoContent);

        // Check companion subtitle was moved and renamed
        var renamedSubPath = Path.Combine(targetDir, "English-Inception-2010.en.srt");
        Assert.True(File.Exists(renamedSubPath), "Subtitle file should have been renamed alongside movie.");

        // Check DB update
        var updated = await _db.Videos.Include(v => v.Category).FirstOrDefaultAsync(v => v.Id == video.Id);
        Assert.NotNull(updated);
        Assert.True(updated.IsOrganized);
        Assert.Equal("Inception", updated.DisplayTitle);
        Assert.Equal("Movies", updated.Category?.Name);
        Assert.Equal("Sci-Fi", updated.PrimaryGenre);
        Assert.Equal(2010, updated.ReleaseYear);
        Assert.Equal("Video", updated.MediaTypeDetected);
    }

    [Fact]
    public async Task OrganizeMediaAsync_WhenAudio_RenamesWithLanguageTitleArtistAndPutsInAudioCategoryFolder()
    {
        // Arrange
        var fakeClient = new FakeSmartRouteClient
        {
            ResultToReturn = new MediaClassificationResult
            {
                MediaType = "Audio",
                Category = "Songs",
                Language = "English",
                CanonicalTitle = "Time",
                ReleaseYear = 2010,
                PrimaryGenre = "Soundtrack",
                Artist = "Hans Zimmer",
                ArtistOrDirector = "Hans Zimmer",
                Summary = "Inception soundtrack composed by Hans Zimmer.",
                SuggestedFileName = "English-Time-Hans-Zimmer"
            }
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Media:RootPath", _tempMediaDir },
                { "Media:Organizer:AutoOrganizeOnDownloadComplete", "true" },
                { "Media:Organizer:GenerateNfoFile", "false" },
                { "Media:Organizer:MoveSubtitles", "false" }
            })
            .Build();

        var organizer = new MediaOrganizerService(_db, fakeClient, config, NullLogger<MediaOrganizerService>.Instance);

        var sourceSubDir = Path.Combine(_tempMediaDir, "Downloads");
        Directory.CreateDirectory(sourceSubDir);
        var sourceFilePath = Path.Combine(sourceSubDir, "audio_12345.mp3");
        await File.WriteAllTextAsync(sourceFilePath, "fake mp3 data");

        var category = new Category { Name = "Downloads", CreatedAt = DateTime.UtcNow };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        var video = new Video
        {
            DisplayTitle = "Hans Zimmer - Time (Official Audio)",
            FileName = "audio_12345.mp3",
            FilePath = sourceFilePath,
            Height = 0,
            CategoryId = category.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Videos.Add(video);
        await _db.SaveChangesAsync();

        // Act
        var result = await organizer.OrganizeMediaAsync(video.Id);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("English-Time-Hans-Zimmer.mp3", result.NewFileName);
        Assert.True(File.Exists(result.NewPath));
        Assert.Contains("Audio" + Path.DirectorySeparatorChar + "Songs", result.NewPath);

        var updated = await _db.Videos.Include(v => v.Category).FirstOrDefaultAsync(v => v.Id == video.Id);
        Assert.NotNull(updated);
        Assert.True(updated.IsOrganized);
        Assert.Equal("Time", updated.DisplayTitle);
        Assert.Equal("Songs", updated.Category?.Name);
        Assert.Equal("Soundtrack", updated.PrimaryGenre);
    }

    [Fact]
    public async Task OrganizeMediaAsync_WhenKannadaMovie_HyphenatesCanonicalTitleAndCategory()
    {
        // Arrange: Kantara 2022 movie
        var fakeClient = new FakeSmartRouteClient
        {
            ResultToReturn = new MediaClassificationResult
            {
                MediaType = "Video",
                Category = "Movies",
                Language = "Kannada",
                CanonicalTitle = "Kantara",
                ReleaseYear = 2022,
                PrimaryGenre = "Action",
                Artist = "Rishab Shetty",
                Summary = "A Kambala champion faces off with an upright DRFO officer.",
                SuggestedFileName = "Kannada-Kantara-2022"
            }
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Media:RootPath", _tempMediaDir },
                { "Media:Organizer:AutoOrganizeOnDownloadComplete", "true" },
                { "Media:Organizer:GenerateNfoFile", "true" },
                { "Media:Organizer:MoveSubtitles", "true" }
            })
            .Build();

        var organizer = new MediaOrganizerService(_db, fakeClient, config, NullLogger<MediaOrganizerService>.Instance);

        var sourceSubDir = Path.Combine(_tempMediaDir, "Downloads");
        Directory.CreateDirectory(sourceSubDir);
        var sourceFilePath = Path.Combine(sourceSubDir, "Kantara.2022.1080p.WEBRip.x264-[YTS.MX].mkv");
        await File.WriteAllTextAsync(sourceFilePath, "fake mkv content");

        var category = new Category { Name = "Downloads", CreatedAt = DateTime.UtcNow };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        var video = new Video
        {
            DisplayTitle = "Kantara.2022.1080p.WEBRip.x264-[YTS.MX]",
            FileName = "Kantara.2022.1080p.WEBRip.x264-[YTS.MX].mkv",
            FilePath = sourceFilePath,
            Height = 1080,
            CategoryId = category.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Videos.Add(video);
        await _db.SaveChangesAsync();

        // Act
        var result = await organizer.OrganizeMediaAsync(video.Id);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Kannada-Kantara-2022.mkv", result.NewFileName);
        Assert.Contains("Videos" + Path.DirectorySeparatorChar + "Movies", result.NewPath);

        var updated = await _db.Videos.Include(v => v.Category).FirstOrDefaultAsync(v => v.Id == video.Id);
        Assert.NotNull(updated);
        Assert.Equal("Movies", updated.Category?.Name);
        Assert.Equal("Kannada", updated.DetectedLanguage);
    }

    [Fact]
    public async Task ConsolidateCategoriesAsync_ConsolidatesLegacyCategoriesAndDeletesEmpty()
    {
        // Arrange: Create fragmented legacy categories matching user screenshot
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Media:RootPath", _tempMediaDir }
            })
            .Build();

        var legacyAudioCarnatic = new Category { Name = "Audio - Carnatic", CreatedAt = DateTime.UtcNow };
        var legacyMoviesAction = new Category { Name = "Movies - Action", CreatedAt = DateTime.UtcNow };
        var emptyMovies = new Category { Name = "Movies", CreatedAt = DateTime.UtcNow };
        var downloads = new Category { Name = "Downloads", CreatedAt = DateTime.UtcNow };

        _db.Categories.AddRange(legacyAudioCarnatic, legacyMoviesAction, emptyMovies, downloads);
        await _db.SaveChangesAsync();

        // Add 1 video in Audio - Carnatic
        var carnaticVideo = new Video
        {
            DisplayTitle = "Vatapi Ganapatim",
            FileName = "vatapi.mp3",
            FilePath = Path.Combine(_tempMediaDir, "vatapi.mp3"),
            CategoryId = legacyAudioCarnatic.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Add 1 video in Movies - Action
        var actionVideo = new Video
        {
            DisplayTitle = "Kantara",
            FileName = "kantara.mkv",
            FilePath = Path.Combine(_tempMediaDir, "kantara.mkv"),
            CategoryId = legacyMoviesAction.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Videos.AddRange(carnaticVideo, actionVideo);
        await _db.SaveChangesAsync();

        var organizer = new MediaOrganizerService(_db, new FakeSmartRouteClient(), config, NullLogger<MediaOrganizerService>.Instance);

        // Act
        var consolidationResult = await organizer.ConsolidateCategoriesAsync();

        // Assert
        Assert.True(consolidationResult.Success);
        Assert.True(consolidationResult.CategoriesMerged >= 2);

        // Check DB categories
        var categoriesRemaining = await _db.Categories.Select(c => c.Name).ToListAsync();
        Assert.DoesNotContain("Audio - Carnatic", categoriesRemaining);
        Assert.DoesNotContain("Movies - Action", categoriesRemaining);
        Assert.Contains("Classical", categoriesRemaining);
        Assert.Contains("Movies", categoriesRemaining);
        Assert.Contains("Downloads", categoriesRemaining);

        // Verify videos reassigned
        var updatedCarnatic = await _db.Videos.Include(v => v.Category).FirstAsync(v => v.Id == carnaticVideo.Id);
        Assert.Equal("Classical", updatedCarnatic.Category?.Name);

        var updatedAction = await _db.Videos.Include(v => v.Category).FirstAsync(v => v.Id == actionVideo.Id);
        Assert.Equal("Movies", updatedAction.Category?.Name);
    }
}
