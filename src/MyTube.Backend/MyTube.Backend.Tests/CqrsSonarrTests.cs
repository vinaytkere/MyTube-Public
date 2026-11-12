using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MyTube.Backend.Application.Features.Sonarr;
using MyTube.Backend.Application.Features.Sonarr.Commands;
using MyTube.Backend.Application.Features.Sonarr.Queries;
using MyTube.Backend.Application.Sonarr;
using Xunit;

namespace MyTube.Backend.Tests;

public class CqrsSonarrTests
{
    private class FakeSonarrService : ISonarrService
    {
        public SonarrSettings Settings { get; set; } = new()
        {
            BaseUrl = "http://localhost:8989",
            ApiKey = "sonarrsecretkey",
            RootFolderPath = "/tv",
            QualityProfileId = 1
        };

        public Task<SonarrStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new SonarrStatusDto { IsConfigured = true, IsConnected = true, Version = "4.0.0" });

        public Task<List<SonarrSeriesDto>> SearchSeriesAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<SonarrSeriesDto> { new() { Id = 1, Title = query } });

        public Task<List<SonarrSeriesDto>> GetLibrarySeriesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<SonarrSeriesDto> { new() { Id = 10, Title = "Better Call Saul" } });

        public Task<SonarrSeriesDto?> GetSeriesByIdAsync(int seriesId, CancellationToken cancellationToken = default)
            => Task.FromResult<SonarrSeriesDto?>(new SonarrSeriesDto { Id = seriesId, Title = "The Wire" });

        public Task<List<SonarrEpisodeDto>> GetEpisodesAsync(int seriesId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<SonarrEpisodeDto> { new() { Id = 1, Title = "Pilot", SeasonNumber = 1, EpisodeNumber = 1 } });

        public Task<SonarrSeriesDto?> AddSeriesAsync(SonarrAddSeriesRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<SonarrSeriesDto?>(new SonarrSeriesDto { Id = 99, Title = request.Title, TvdbId = request.TvdbId });

        public Task<List<SonarrReleaseDto>> GetReleasesAsync(int seriesId, int? episodeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<SonarrReleaseDto> { new() { Guid = "rel-1", Title = "Episode 1 1080p" } });

        public Task<bool> DownloadReleaseAsync(SonarrDownloadReleaseRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<List<SonarrQueueItemDto>> GetQueueAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<SonarrQueueItemDto> { new() { Id = 1, SeriesTitle = "Dark", Progress = 50.0 } });

        public Task<List<SonarrQualityProfileDto>> GetQualityProfilesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<SonarrQualityProfileDto> { new() { Id = 1, Name = "HD - 1080p" } });

        public Task<List<SonarrRootFolderDto>> GetRootFoldersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<SonarrRootFolderDto> { new() { Id = 1, Path = "/tv", FreeSpace = 2000000 } });

        public Task<SonarrStatusDto> TestConnectionAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default)
            => Task.FromResult(new SonarrStatusDto { IsConnected = true, IsConfigured = true });

        public Task UpdateSettingsAsync(SonarrSettings newSettings, CancellationToken cancellationToken = default)
        {
            Settings = newSettings;
            return Task.CompletedTask;
        }

        public SonarrSettings GetCurrentSettings() => Settings;
    }

    private readonly FakeSonarrService _sonarrService = new();

    [Fact]
    public async Task GetSonarrStatusQueryHandler_ReturnsStatus()
    {
        var handler = new GetSonarrStatusQueryHandler(_sonarrService);
        var result = await handler.Handle(new GetSonarrStatusQuery(), CancellationToken.None);

        result.Should().NotBeNull();
        result.IsConfigured.Should().BeTrue();
        result.Version.Should().Be("4.0.0");
    }

    [Fact]
    public void SearchSonarrSeriesQueryValidator_ValidatesQuery()
    {
        var validator = new SearchSonarrSeriesQueryValidator();
        validator.Validate(new SearchSonarrSeriesQuery("")).IsValid.Should().BeFalse();
        validator.Validate(new SearchSonarrSeriesQuery("Breaking Bad")).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task SearchSonarrSeriesQueryHandler_ReturnsSeries()
    {
        var handler = new SearchSonarrSeriesQueryHandler(_sonarrService);
        var result = await handler.Handle(new SearchSonarrSeriesQuery("Breaking Bad"), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Breaking Bad");
    }

    [Fact]
    public async Task GetSonarrLibraryQueryHandler_ReturnsSeriesList()
    {
        var handler = new GetSonarrLibraryQueryHandler(_sonarrService);
        var result = await handler.Handle(new GetSonarrLibraryQuery(), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Better Call Saul");
    }

    [Fact]
    public void GetSonarrSeriesByIdQueryValidator_ValidatesSeriesId()
    {
        var validator = new GetSonarrSeriesByIdQueryValidator();
        validator.Validate(new GetSonarrSeriesByIdQuery(0)).IsValid.Should().BeFalse();
        validator.Validate(new GetSonarrSeriesByIdQuery(5)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task GetSonarrSeriesByIdQueryHandler_ReturnsSeries()
    {
        var handler = new GetSonarrSeriesByIdQueryHandler(_sonarrService);
        var result = await handler.Handle(new GetSonarrSeriesByIdQuery(5), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Title.Should().Be("The Wire");
    }

    [Fact]
    public void GetSonarrEpisodesQueryValidator_ValidatesSeriesId()
    {
        var validator = new GetSonarrEpisodesQueryValidator();
        validator.Validate(new GetSonarrEpisodesQuery(0)).IsValid.Should().BeFalse();
        validator.Validate(new GetSonarrEpisodesQuery(10)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task GetSonarrEpisodesQueryHandler_ReturnsEpisodes()
    {
        var handler = new GetSonarrEpisodesQueryHandler(_sonarrService);
        var result = await handler.Handle(new GetSonarrEpisodesQuery(10), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Pilot");
    }

    [Fact]
    public void AddSonarrSeriesCommandValidator_ValidatesTvdbIdAndTitle()
    {
        var validator = new AddSonarrSeriesCommandValidator();
        var invalid = validator.Validate(new AddSonarrSeriesCommand(new SonarrAddSeriesRequest { TvdbId = 0, Title = "" }));
        var valid = validator.Validate(new AddSonarrSeriesCommand(new SonarrAddSeriesRequest { TvdbId = 12345, Title = "New Series" }));

        invalid.IsValid.Should().BeFalse();
        valid.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task AddSonarrSeriesCommandHandler_CallsService()
    {
        var req = new SonarrAddSeriesRequest { TvdbId = 12345, Title = "New Series" };
        var handler = new AddSonarrSeriesCommandHandler(_sonarrService);
        var result = await handler.Handle(new AddSonarrSeriesCommand(req), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(99);
    }

    [Fact]
    public void GetSonarrReleasesQueryValidator_ValidatesSeriesId()
    {
        var validator = new GetSonarrReleasesQueryValidator();
        validator.Validate(new GetSonarrReleasesQuery(0)).IsValid.Should().BeFalse();
        validator.Validate(new GetSonarrReleasesQuery(20)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task GetSonarrReleasesQueryHandler_ReturnsReleases()
    {
        var handler = new GetSonarrReleasesQueryHandler(_sonarrService);
        var result = await handler.Handle(new GetSonarrReleasesQuery(20), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Guid.Should().Be("rel-1");
    }

    [Fact]
    public void DownloadSonarrReleaseCommandValidator_ValidatesGuid()
    {
        var validator = new DownloadSonarrReleaseCommandValidator();
        validator.Validate(new DownloadSonarrReleaseCommand(new SonarrDownloadReleaseRequest { Guid = "" })).IsValid.Should().BeFalse();
        validator.Validate(new DownloadSonarrReleaseCommand(new SonarrDownloadReleaseRequest { Guid = "rel-guid" })).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task DownloadSonarrReleaseCommandHandler_CallsService()
    {
        var req = new SonarrDownloadReleaseRequest { Guid = "rel-guid", IndexerId = 1 };
        var handler = new DownloadSonarrReleaseCommandHandler(_sonarrService);
        var result = await handler.Handle(new DownloadSonarrReleaseCommand(req), CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetSonarrQueueQueryHandler_ReturnsQueue()
    {
        var handler = new GetSonarrQueueQueryHandler(_sonarrService);
        var result = await handler.Handle(new GetSonarrQueueQuery(), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].SeriesTitle.Should().Be("Dark");
    }

    [Fact]
    public async Task GetSonarrSettingsQueryHandler_MasksApiKey()
    {
        var handler = new GetSonarrSettingsQueryHandler(_sonarrService);
        var result = await handler.Handle(new GetSonarrSettingsQuery(), CancellationToken.None);

        result.BaseUrl.Should().Be("http://localhost:8989");
        result.ApiKey.Should().Be("sona****");
        result.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task TestSonarrConnectionCommandHandler_UsesFallbackKeyWhenMasked()
    {
        var handler = new TestSonarrConnectionCommandHandler(_sonarrService);
        var result = await handler.Handle(new TestSonarrConnectionCommand(new SonarrTestConnectionRequest
        {
            BaseUrl = "http://localhost:8989",
            ApiKey = "sona****"
        }), CancellationToken.None);

        result.IsConnected.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateSonarrSettingsCommandHandler_CallsService()
    {
        var settings = new SonarrSettings { BaseUrl = "http://localhost:8989", ApiKey = "new-key" };
        var handler = new UpdateSonarrSettingsCommandHandler(_sonarrService);
        var result = await handler.Handle(new UpdateSonarrSettingsCommand(settings), CancellationToken.None);

        result.Should().BeTrue();
        _sonarrService.Settings.ApiKey.Should().Be("new-key");
    }
}
