using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MyTube.Backend.Application.FileNaming;
using MyTube.Backend.Domain;
using MyTube.Backend.Infrastructure;
using MyTube.Backend.Infrastructure.FileNaming;
using Xunit;

namespace MyTube.Backend.Tests;

public class MediaNamingServiceTests
{
    private MediaNamingService CreateService()
    {
        var options = new DbContextOptionsBuilder<VideoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var db = new VideoDbContext(options);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Media:RootPath"] = "./test_media"
            })
            .Build();

        return new MediaNamingService(db, config, NullLogger<MediaNamingService>.Instance);
    }

    [Fact]
    public void FormatFileName_ReplacesStandardTokensCorrectly()
    {
        var service = CreateService();
        var video = new Video
        {
            DisplayTitle = "Inception",
            FileName = "inception.raw.mp4",
            Height = 1080,
            Width = 1920,
            VideoCodec = "h264",
            AudioCodec = "aac",
            CreatedAt = new DateTime(2010, 7, 16)
        };

        var result = service.FormatFileName(
            video,
            "{Title} [{Quality}] [{VideoCodec}]",
            SpaceReplacementOption.Space,
            ColonReplacementOption.Dash
        );

        Assert.Equal("Inception [1080p] [h264]", result);
    }

    [Fact]
    public void FormatFileName_HandlesDotSpaceReplacement()
    {
        var service = CreateService();
        var video = new Video
        {
            DisplayTitle = "The Dark Knight",
            FileName = "tdk.mp4",
            Height = 2160,
            Width = 3840,
            VideoCodec = "hevc",
            CreatedAt = new DateTime(2008, 7, 18)
        };

        var result = service.FormatFileName(
            video,
            "{CleanTitle} {Year} [{Quality}]",
            SpaceReplacementOption.Dot,
            ColonReplacementOption.Dash
        );

        Assert.Equal("The.Dark.Knight.2008.[4K]", result);
    }

    [Fact]
    public void FormatFileName_HandlesCategoryFolderPrefix()
    {
        var service = CreateService();
        var category = new Category { Id = 1, Name = "Documentaries" };
        var video = new Video
        {
            DisplayTitle = "Planet Earth",
            FileName = "pe.mkv",
            Category = category,
            Height = 1080,
            VideoCodec = "h264"
        };

        var result = service.FormatFileName(
            video,
            "{Category}/{Title} [{Quality}]",
            SpaceReplacementOption.Space,
            ColonReplacementOption.Dash
        );

        Assert.Equal("Documentaries/Planet Earth [1080p]", result);
    }
}
