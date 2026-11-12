using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MyTube.Backend.Application.Features.Radarr;
using MyTube.Backend.Application.Features.Radarr.Commands;
using MyTube.Backend.Application.Features.Radarr.Queries;
using MyTube.Backend.Application.Prowlarr;
using MyTube.Backend.Application.Radarr;
using Xunit;

namespace MyTube.Backend.Tests;

public class CqrsRadarrTests
{
    private class FakeRadarrService : IRadarrService
    {
        public RadarrSettings Settings { get; set; } = new()
        {
            BaseUrl = "http://localhost:7878",
            ApiKey = "secret123456",
            RootFolderPath = "/movies",
            QualityProfileId = 1
        };

        public Task<RadarrStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new RadarrStatusDto { IsConfigured = true, IsConnected = true, Version = "5.0.0" });

        public Task<List<RadarrMovieDto>> SearchMoviesAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<RadarrMovieDto> { new() { Id = 1, Title = query, Year = 2010 } });

        public Task<List<RadarrMovieDto>> GetLibraryMoviesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<RadarrMovieDto> { new() { Id = 10, Title = "Interstellar" } });

        public Task<RadarrMovieDto?> AddMovieAsync(RadarrAddMovieRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<RadarrMovieDto?>(new RadarrMovieDto { Id = 5, Title = request.Title, TmdbId = request.TmdbId });

        public Task<RadarrAutoGrabResultDto> AutoGrabMovieAsync(RadarrAddMovieRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new RadarrAutoGrabResultDto { Success = true, Grabbed = true, ReleaseTitle = "Auto.Movie.1080p" });

        public Task<List<RadarrReleaseDto>> GetReleasesAsync(int movieId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<RadarrReleaseDto> { new() { Guid = "abc-123", Title = "Release 1" } });

        public Task<bool> DownloadReleaseAsync(RadarrDownloadReleaseRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<List<RadarrQueueItemDto>> GetQueueAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<RadarrQueueItemDto> { new() { Id = 1, MovieTitle = "Inception" } });

        public Task<bool> CancelQueueItemAsync(int id, int? transmissionId = null, bool deleteData = true, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> PauseQueueItemAsync(int id, int? transmissionId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> ResumeQueueItemAsync(int id, int? transmissionId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> KickstartQueueItemAsync(int id, int? transmissionId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<List<RadarrQualityProfileDto>> GetQualityProfilesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<RadarrQualityProfileDto> { new() { Id = 1, Name = "HD - 1080p" } });

        public Task<List<RadarrRootFolderDto>> GetRootFoldersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<RadarrRootFolderDto> { new() { Id = 1, Path = "/movies", FreeSpace = 1000000 } });

        public Task<RadarrStatusDto> TestConnectionAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default)
            => Task.FromResult(new RadarrStatusDto { IsConnected = true, IsConfigured = true });

        public Task UpdateSettingsAsync(RadarrSettings newSettings, CancellationToken cancellationToken = default)
        {
            Settings = newSettings;
            return Task.CompletedTask;
        }

        public RadarrSettings GetCurrentSettings() => Settings;

        public Task<RadarrDownloadSummaryDto> GetDownloadedMediaSummaryAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new RadarrDownloadSummaryDto { TotalDownloadedCount = 1, TotalSizeBytes = 1000 });

        public Task<bool> SyncRadarrMediaLinksAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> DeleteMovieFilesAsync(int movieId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<int> DeleteAllDownloadedFilesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(5);

        public Task<int> BatchDeleteMovieFilesAsync(List<int> movieIds, List<string>? filePaths = null, CancellationToken cancellationToken = default)
            => Task.FromResult(movieIds.Count);

        public Task<(string FilePath, string ContentType, string FileName)?> GetMovieDownloadFileAsync(int movieId, CancellationToken cancellationToken = default)
            => Task.FromResult<(string FilePath, string ContentType, string FileName)?>(("/movies/sample.mp4", "video/mp4", "sample.mp4"));

        public Task<(string FilePath, string ContentType, string FileName)?> GetFileDownloadAsync(string filePath, CancellationToken cancellationToken = default)
            => Task.FromResult<(string FilePath, string ContentType, string FileName)?>(("/movies/sample.mp4", "video/mp4", "sample.mp4"));

        public Task<(string ResolvedPath, bool IsDirectory, string ContentType, string FileName)?> GetDownloadTargetAsync(string path, CancellationToken cancellationToken = default)
            => Task.FromResult<(string ResolvedPath, bool IsDirectory, string ContentType, string FileName)?>(("/movies/sample.mp4", false, "video/mp4", "sample.mp4"));

        public Task<(string? TargetPath, bool IsDirectory, List<string> Files, string DownloadName, string? ErrorMessage)?> GetTransmissionDownloadInfoAsync(int transmissionId, int? fileIndex = null, int? movieId = null, CancellationToken cancellationToken = default)
            => Task.FromResult<(string? TargetPath, bool IsDirectory, List<string> Files, string DownloadName, string? ErrorMessage)?>(("/torrents/dl", false, new List<string> { "/torrents/dl/file.mkv" }, "Torrent1", null));

        public Task<(byte[] ImageBytes, string ContentType, string FileName)?> GetMoviePosterDownloadAsync(int movieId, CancellationToken cancellationToken = default)
            => Task.FromResult<(byte[] ImageBytes, string ContentType, string FileName)?>(((byte[])new byte[] { 1, 2, 3 }, "image/jpeg", "poster.jpg"));

        public Task<(byte[] ImageBytes, string ContentType, string FileName)?> GetMovieFanartDownloadAsync(int movieId, CancellationToken cancellationToken = default)
            => Task.FromResult<(byte[] ImageBytes, string ContentType, string FileName)?>(((byte[])new byte[] { 4, 5, 6 }, "image/jpeg", "fanart.jpg"));
    }

    private class FakeProwlarrService : IProwlarrService
    {
        public Task<List<ProwlarrSearchResultDto>> SearchAsync(string query, string? category = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<ProwlarrSearchResultDto> { new() { Title = query, Guid = "iso-1" } });

        public Task<bool> DownloadAsync(ProwlarrDownloadRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<List<TransmissionTorrentDto>> GetTransmissionTorrentsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<TransmissionTorrentDto>());

        public Task<bool> RemoveTransmissionTorrentAsync(int torrentId, bool deleteData = true, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> PauseTransmissionTorrentAsync(int torrentId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> ResumeTransmissionTorrentAsync(int torrentId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> KickstartTransmissionTorrentAsync(int torrentId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public string? ResolveCachedTitle(string? hashOrName) => hashOrName;
    }

    private readonly FakeRadarrService _radarrService = new();
    private readonly FakeProwlarrService _prowlarrService = new();

    [Fact]
    public async Task GetRadarrStatusQueryHandler_ReturnsStatus()
    {
        var handler = new GetRadarrStatusQueryHandler(_radarrService);
        var result = await handler.Handle(new GetRadarrStatusQuery(), CancellationToken.None);

        result.Should().NotBeNull();
        result.IsConfigured.Should().BeTrue();
        result.Version.Should().Be("5.0.0");
    }

    [Fact]
    public void SearchRadarrMoviesQueryValidator_ValidatesQuery()
    {
        var validator = new SearchRadarrMoviesQueryValidator();
        var emptyResult = validator.Validate(new SearchRadarrMoviesQuery(""));
        var validResult = validator.Validate(new SearchRadarrMoviesQuery("Inception"));

        emptyResult.IsValid.Should().BeFalse();
        validResult.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task SearchRadarrMoviesQueryHandler_ReturnsMovies()
    {
        var handler = new SearchRadarrMoviesQueryHandler(_radarrService);
        var result = await handler.Handle(new SearchRadarrMoviesQuery("Inception"), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Inception");
    }

    [Fact]
    public async Task GetRadarrLibraryQueryHandler_ReturnsLibrary()
    {
        var handler = new GetRadarrLibraryQueryHandler(_radarrService);
        var result = await handler.Handle(new GetRadarrLibraryQuery(), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Interstellar");
    }

    [Fact]
    public void AddRadarrMovieCommandValidator_ValidatesTmdbIdAndTitle()
    {
        var validator = new AddRadarrMovieCommandValidator();
        var invalid = validator.Validate(new AddRadarrMovieCommand(new RadarrAddMovieRequest { TmdbId = 0, Title = "" }));
        var valid = validator.Validate(new AddRadarrMovieCommand(new RadarrAddMovieRequest { TmdbId = 1234, Title = "Movie Title" }));

        invalid.IsValid.Should().BeFalse();
        valid.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task AddRadarrMovieCommandHandler_CallsService()
    {
        var request = new RadarrAddMovieRequest { TmdbId = 1234, Title = "Movie Title" };
        var handler = new AddRadarrMovieCommandHandler(_radarrService);
        var result = await handler.Handle(new AddRadarrMovieCommand(request), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(5);
    }

    [Fact]
    public async Task AutoGrabRadarrMovieCommandHandler_CallsService()
    {
        var request = new RadarrAddMovieRequest { TmdbId = 999, Title = "Auto Movie" };
        var handler = new AutoGrabRadarrMovieCommandHandler(_radarrService);
        var result = await handler.Handle(new AutoGrabRadarrMovieCommand(request), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Grabbed.Should().BeTrue();
        result.ReleaseTitle.Should().Be("Auto.Movie.1080p");
    }

    [Fact]
    public void GetRadarrReleasesQueryValidator_ValidatesMovieId()
    {
        var validator = new GetRadarrReleasesQueryValidator();
        validator.Validate(new GetRadarrReleasesQuery(0)).IsValid.Should().BeFalse();
        validator.Validate(new GetRadarrReleasesQuery(12)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task GetRadarrReleasesQueryHandler_ReturnsReleases()
    {
        var handler = new GetRadarrReleasesQueryHandler(_radarrService);
        var result = await handler.Handle(new GetRadarrReleasesQuery(12), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Guid.Should().Be("abc-123");
    }

    [Fact]
    public void DownloadRadarrReleaseCommandValidator_ValidatesGuid()
    {
        var validator = new DownloadRadarrReleaseCommandValidator();
        validator.Validate(new DownloadRadarrReleaseCommand(new RadarrDownloadReleaseRequest { Guid = "" })).IsValid.Should().BeFalse();
        validator.Validate(new DownloadRadarrReleaseCommand(new RadarrDownloadReleaseRequest { Guid = "some-guid" })).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ManageRadarrQueueCommandHandlers_ExecuteSuccessfully()
    {
        var cancelHandler = new CancelRadarrQueueItemCommandHandler(_radarrService);
        var pauseHandler = new PauseRadarrQueueItemCommandHandler(_radarrService);
        var resumeHandler = new ResumeRadarrQueueItemCommandHandler(_radarrService);
        var kickstartHandler = new KickstartRadarrQueueItemCommandHandler(_radarrService);

        (await cancelHandler.Handle(new CancelRadarrQueueItemCommand(1, 10, true), CancellationToken.None)).Should().BeTrue();
        (await pauseHandler.Handle(new PauseRadarrQueueItemCommand(2, 20), CancellationToken.None)).Should().BeTrue();
        (await resumeHandler.Handle(new ResumeRadarrQueueItemCommand(3, 30), CancellationToken.None)).Should().BeTrue();
        (await kickstartHandler.Handle(new KickstartRadarrQueueItemCommand(4, 40), CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public void ManageRadarrQueueCommandValidators_AllowValidIdOrTransmissionId()
    {
        var cancelValidator = new CancelRadarrQueueItemCommandValidator();
        var pauseValidator = new PauseRadarrQueueItemCommandValidator();
        var resumeValidator = new ResumeRadarrQueueItemCommandValidator();
        var kickstartValidator = new KickstartRadarrQueueItemCommandValidator();

        // 1. Direct Transmission torrents where Radarr queue Id is 0 but TransmissionId > 0
        cancelValidator.Validate(new CancelRadarrQueueItemCommand(0, 15)).IsValid.Should().BeTrue();
        pauseValidator.Validate(new PauseRadarrQueueItemCommand(0, 15)).IsValid.Should().BeTrue();
        resumeValidator.Validate(new ResumeRadarrQueueItemCommand(0, 15)).IsValid.Should().BeTrue();
        kickstartValidator.Validate(new KickstartRadarrQueueItemCommand(0, 15)).IsValid.Should().BeTrue();

        // 2. Standard Radarr items where Id > 0 and TransmissionId is null
        cancelValidator.Validate(new CancelRadarrQueueItemCommand(5, null)).IsValid.Should().BeTrue();
        pauseValidator.Validate(new PauseRadarrQueueItemCommand(5, null)).IsValid.Should().BeTrue();
        resumeValidator.Validate(new ResumeRadarrQueueItemCommand(5, null)).IsValid.Should().BeTrue();
        kickstartValidator.Validate(new KickstartRadarrQueueItemCommand(5, null)).IsValid.Should().BeTrue();

        // 3. Invalid items where both Id <= 0 and TransmissionId is missing or <= 0
        var cancelRes = cancelValidator.Validate(new CancelRadarrQueueItemCommand(0, null));
        cancelRes.IsValid.Should().BeFalse();
        cancelRes.Errors[0].ErrorMessage.Should().Contain("Valid queue item ID or transmission ID is required.");

        pauseValidator.Validate(new PauseRadarrQueueItemCommand(0, 0)).IsValid.Should().BeFalse();
        resumeValidator.Validate(new ResumeRadarrQueueItemCommand(0, null)).IsValid.Should().BeFalse();
        kickstartValidator.Validate(new KickstartRadarrQueueItemCommand(0, 0)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task GetRadarrSettingsQueryHandler_MasksApiKey()
    {
        var handler = new GetRadarrSettingsQueryHandler(_radarrService);
        var result = await handler.Handle(new GetRadarrSettingsQuery(), CancellationToken.None);

        result.BaseUrl.Should().Be("http://localhost:7878");
        result.ApiKey.Should().Be("secr****");
        result.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task TestRadarrConnectionCommandHandler_UsesFallbackKeyWhenMasked()
    {
        var handler = new TestRadarrConnectionCommandHandler(_radarrService);
        var result = await handler.Handle(new TestRadarrConnectionCommand(new RadarrTestConnectionRequest
        {
            BaseUrl = "http://localhost:7878",
            ApiKey = "secr****"
        }), CancellationToken.None);

        result.IsConnected.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteRadarrMovieFilesCommandHandler_DeletesFiles()
    {
        var handler = new DeleteRadarrMovieFilesCommandHandler(_radarrService);
        var result = await handler.Handle(new DeleteRadarrMovieFilesCommand(55), CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task BatchDeleteRadarrFilesCommandHandler_DeletesBatch()
    {
        var handler = new BatchDeleteRadarrFilesCommandHandler(_radarrService);
        var result = await handler.Handle(new BatchDeleteRadarrFilesCommand(new List<int> { 1, 2 }, null), CancellationToken.None);

        result.Should().Be(2);
    }

    [Fact]
    public async Task SearchProwlarrIndexersQueryHandler_CallsProwlarrService()
    {
        var handler = new SearchProwlarrIndexersQueryHandler(_prowlarrService);
        var result = await handler.Handle(new SearchProwlarrIndexersQuery("Linux"), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Linux");
    }

    [Fact]
    public async Task DownloadProwlarrReleaseCommandHandler_CallsProwlarrService()
    {
        var req = new ProwlarrDownloadRequest { MagnetUrl = "magnet:?xt=urn:btih:abc" };
        var handler = new DownloadProwlarrReleaseCommandHandler(_prowlarrService);
        var result = await handler.Handle(new DownloadProwlarrReleaseCommand(req), CancellationToken.None);

        result.Should().BeTrue();
    }
}
