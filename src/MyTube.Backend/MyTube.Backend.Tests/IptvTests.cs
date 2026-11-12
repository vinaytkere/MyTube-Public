using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MyTube.Backend.Application.Features.Iptv.Commands;
using MyTube.Backend.Application.Features.Iptv.Queries;
using MyTube.Backend.Application.Iptv;
using MyTube.Backend.Domain;
using MyTube.Backend.Infrastructure;
using MyTube.Backend.Infrastructure.Iptv;
using Xunit;

namespace MyTube.Backend.Tests;

public class IptvTests
{
    private VideoDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<VideoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new VideoDbContext(options);
    }

    // === 1. SSRF Validator Tests ===

    [Theory]
    [InlineData("http://127.0.0.1/live.m3u8", false)]
    [InlineData("http://localhost:5000/live.m3u8", false)]
    [InlineData("http://10.0.1.5/stream.m3u8", false)]
    [InlineData("http://192.168.1.1:8080/live.m3u8", false)]
    [InlineData("http://172.16.0.10/live.m3u8", false)]
    [InlineData("http://169.254.169.254/metadata", false)]
    [InlineData("file:///etc/passwd", false)]
    [InlineData("ftp://stream.server.com/test", false)]
    [InlineData("https://8.8.8.8/hls/live.m3u8", true)]
    public async Task SsrfValidator_ShouldBlockUnsafeUrlsAndAllowSafeUrls(string url, bool expectedSafe)
    {
        var validator = new SsrfValidator(NullLogger<SsrfValidator>.Instance);
        var result = await validator.ValidateUrlAsync(url);

        result.IsSafe.Should().Be(expectedSafe);
    }

    // === 2. IPTV Service Tests ===

    [Fact]
    public async Task GetChannelsAsync_ShouldReturnActiveChannelsWithStreams()
    {
        using var db = CreateInMemoryDbContext();
        var service = new IptvService(db, NullLogger<IptvService>.Instance);

        var country = new IptvCountry { Code = "US", Name = "United States", Flag = "🇺🇸" };
        var category = new IptvCategory { Id = "news", Name = "News" };
        db.IptvCountries.Add(country);
        db.IptvCategories.Add(category);

        var channel = new IptvChannel
        {
            Id = "BBCNews.uk",
            Name = "BBC News",
            CountryCode = "US",
            IsActive = true,
            IsClosed = false
        };
        channel.ChannelCategories.Add(new IptvChannelCategory { ChannelId = channel.Id, CategoryId = "news" });
        channel.Streams.Add(new IptvStream
        {
            ChannelId = channel.Id,
            Url = "https://stream.bbc.co.uk/live.m3u8",
            Quality = "1080p",
            IsActive = true
        });

        db.IptvChannels.Add(channel);
        await db.SaveChangesAsync();

        var result = await service.GetChannelsAsync(
            userId: null,
            scope: "worldwide",
            regionCode: null,
            countryCode: null,
            languageCode: null,
            categoryId: null,
            search: null,
            favoritesOnly: false,
            historyOnly: false,
            page: 1,
            pageSize: 20);

        result.TotalCount.Should().Be(1);
        result.Items.Should().HaveCount(1);
        result.Items[0].Id.Should().Be("BBCNews.uk");
        result.Items[0].Streams.Should().HaveCount(1);
        result.Items[0].Streams[0].Quality.Should().Be("1080p");
    }

    [Fact]
    public async Task GetChannelsAsync_Search_ShouldFilterByName()
    {
        using var db = CreateInMemoryDbContext();
        var service = new IptvService(db, NullLogger<IptvService>.Instance);

        var ch1 = new IptvChannel { Id = "cnn.us", Name = "CNN International", IsActive = true };
        ch1.Streams.Add(new IptvStream { ChannelId = ch1.Id, Url = "https://cnn.com/live.m3u8", IsActive = true });

        var ch2 = new IptvChannel { Id = "aljazeera.qa", Name = "Al Jazeera English", IsActive = true };
        ch2.Streams.Add(new IptvStream { ChannelId = ch2.Id, Url = "https://aj.com/live.m3u8", IsActive = true });

        db.IptvChannels.AddRange(ch1, ch2);
        await db.SaveChangesAsync();

        var result = await service.GetChannelsAsync(
            userId: null, scope: "worldwide", regionCode: null, countryCode: null,
            languageCode: null, categoryId: null, search: "Jazeera",
            favoritesOnly: false, historyOnly: false, page: 1, pageSize: 20);

        result.TotalCount.Should().Be(1);
        result.Items[0].Id.Should().Be("aljazeera.qa");
    }

    [Fact]
    public async Task ToggleFavoriteAsync_ShouldAddAndRemoveFavorite()
    {
        using var db = CreateInMemoryDbContext();
        var service = new IptvService(db, NullLogger<IptvService>.Instance);

        var ch = new IptvChannel { Id = "euronews.fr", Name = "Euronews", IsActive = true };
        ch.Streams.Add(new IptvStream { ChannelId = ch.Id, Url = "https://euronews.com/live.m3u8", IsActive = true });
        db.IptvChannels.Add(ch);
        await db.SaveChangesAsync();

        var added = await service.ToggleFavoriteAsync("user123", "euronews.fr");
        added.Should().BeTrue();

        var favs = await service.GetUserFavoriteChannelIdsAsync("user123");
        favs.Should().Contain("euronews.fr");

        var removed = await service.ToggleFavoriteAsync("user123", "euronews.fr");
        removed.Should().BeFalse();

        var favsAfter = await service.GetUserFavoriteChannelIdsAsync("user123");
        favsAfter.Should().NotContain("euronews.fr");
    }

    [Fact]
    public async Task RecordWatchHistoryAsync_ShouldTrackRecentPlayback()
    {
        using var db = CreateInMemoryDbContext();
        var service = new IptvService(db, NullLogger<IptvService>.Instance);

        var ch = new IptvChannel { Id = "sky.uk", Name = "Sky News", IsActive = true };
        ch.Streams.Add(new IptvStream { Id = 101, ChannelId = ch.Id, Url = "https://sky.com/live.m3u8", IsActive = true });
        db.IptvChannels.Add(ch);
        await db.SaveChangesAsync();

        await service.RecordWatchHistoryAsync("user123", "sky.uk", 101);

        var channel = await service.GetChannelByIdAsync("sky.uk", "user123");
        channel.Should().NotBeNull();
        channel!.LastWatchedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ReportStreamHealthAsync_ShouldUpdateStatus()
    {
        using var db = CreateInMemoryDbContext();
        var service = new IptvService(db, NullLogger<IptvService>.Instance);

        var stream = new IptvStream { Id = 42, ChannelId = "france24.fr", Url = "https://f24.com/live.m3u8", Status = "Unknown", IsActive = true };
        db.IptvStreams.Add(stream);
        await db.SaveChangesAsync();

        var success = await service.ReportStreamHealthAsync(42, isWorking: false, errorMessage: "HTTP 404");
        success.Should().BeTrue();

        var updated = await db.IptvStreams.FindAsync(42);
        updated!.Status.Should().Be("Offline");
        updated.LastCheckedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateM3uPlaylistAsync_ShouldOutputValidM3uFormat()
    {
        using var db = CreateInMemoryDbContext();
        var service = new IptvService(db, NullLogger<IptvService>.Instance);

        var channel = new IptvChannel
        {
            Id = "BBCNews.uk",
            Name = "BBC News",
            CountryCode = "GB",
            LogoUrl = "https://i.imgur.com/bbc.png",
            IsActive = true,
            IsClosed = false
        };
        channel.ChannelCategories.Add(new IptvChannelCategory
        {
            ChannelId = channel.Id,
            CategoryId = "news",
            Category = new IptvCategory { Id = "news", Name = "News" }
        });
        channel.Streams.Add(new IptvStream
        {
            ChannelId = channel.Id,
            Url = "https://stream.bbc.co.uk/live.m3u8",
            Quality = "1080p",
            IsActive = true,
            OrderIndex = 1
        });

        db.IptvChannels.Add(channel);
        await db.SaveChangesAsync();

        var m3u = await service.GenerateM3uPlaylistAsync(
            userId: null,
            favoritesOnly: false,
            countryCode: null,
            categoryId: null,
            languageCode: null,
            httpsOnly: false);

        m3u.Should().StartWith("#EXTM3U");
        m3u.Should().Contain("tvg-id=\"BBCNews.uk\"");
        m3u.Should().Contain("tvg-name=\"BBC News\"");
        m3u.Should().Contain("tvg-logo=\"https://i.imgur.com/bbc.png\"");
        m3u.Should().Contain("group-title=\"News\"");
        m3u.Should().Contain("BBC News");
        m3u.Should().Contain("https://stream.bbc.co.uk/live.m3u8");
    }

    [Fact]
    public void IptvStreamRelayService_RewriteManifest_ShouldRewriteRelativeAndAbsoluteUris()
    {
        var relay = new IptvStreamRelayService(null!, null!, NullLogger<IptvStreamRelayService>.Instance);
        var baseUri = new Uri("http://stream.example.com/live/index.m3u8");

        var rawManifest = "#EXTM3U\n" +
                          "#EXT-X-VERSION:3\n" +
                          "#EXT-X-TARGETDURATION:10\n" +
                          "#EXTINF:10.0,\n" +
                          "segment1.ts\n" +
                          "#EXT-X-KEY:METHOD=AES-128,URI=\"enc.key\"\n" +
                          "#EXTINF:10.0,\n" +
                          "http://cdn.example.com/segment2.ts\n";

        var rewritten = relay.RewriteManifest(rawManifest, baseUri);

        rewritten.Should().Contain("/api/iptv/stream-relay?url=http%3A%2F%2Fstream.example.com%2Flive%2Fsegment1.ts");
        rewritten.Should().Contain("/api/iptv/stream-relay?url=http%3A%2F%2Fstream.example.com%2Flive%2Fenc.key");
        rewritten.Should().Contain("/api/iptv/stream-relay?url=http%3A%2F%2Fcdn.example.com%2Fsegment2.ts");
    }

    // === 3. CQRS Validators Tests ===

    [Theory]
    [InlineData(0, 20, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 101, false)]
    [InlineData(1, 30, true)]
    public void GetIptvChannelsQueryValidator_ShouldValidatePagination(int page, int pageSize, bool isValid)
    {
        var validator = new GetIptvChannelsQueryValidator();
        var query = new GetIptvChannelsQuery(null, null, null, null, null, null, null, false, false, page, pageSize);
        var result = validator.Validate(query);

        result.IsValid.Should().Be(isValid);
    }
}
