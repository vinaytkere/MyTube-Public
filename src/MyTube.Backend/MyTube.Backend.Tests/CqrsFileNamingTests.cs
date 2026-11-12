using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MyTube.Backend.Application.Features.FileNaming.Commands;
using MyTube.Backend.Application.Features.FileNaming.Queries;
using MyTube.Backend.Application.FileNaming;
using MyTube.Backend.Domain;
using Xunit;

namespace MyTube.Backend.Tests;

public class CqrsFileNamingTests
{
    private class FakeMediaNamingService : IMediaNamingService
    {
        private FileNamingSettingsDto _settings = new();

        public FileNamingSettingsDto GetSettings() => _settings;

        public Task UpdateSettingsAsync(FileNamingSettingsDto settings, CancellationToken cancellationToken = default)
        {
            _settings = settings;
            return Task.CompletedTask;
        }

        public string FormatFileName(Video video, string pattern, SpaceReplacementOption spaceReplacement, ColonReplacementOption colonReplacement)
            => $"{video.DisplayTitle} [1080p]";

        public Task<FileRenamePreviewResultDto> PreviewRenamingAsync(FileRenamePreviewRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new FileRenamePreviewResultDto
            {
                Pattern = request.Pattern ?? "{Title}",
                TotalVideos = 5,
                VideosToRename = 3,
                ConflictsCount = 0
            });
        }

        public Task<FileRenameExecuteResultDto> ExecuteRenamingAsync(FileRenameExecuteRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new FileRenameExecuteResultDto
            {
                Success = true,
                RenamedCount = 3,
                ErrorCount = 0
            });
        }

        public Task<RecycleBinSummaryDto> GetRecycleBinSummaryAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RecycleBinSummaryDto
            {
                FileCount = 4,
                TotalSizeBytes = 1048576,
                FormattedTotalSize = "1.0 MB"
            });
        }

        public Task<int> EmptyRecycleBinAsync(CancellationToken cancellationToken = default) => Task.FromResult(4);
    }

    [Fact]
    public async Task GetFileNamingSettingsQueryHandler_ShouldReturnSettingsAndPresets()
    {
        var service = new FakeMediaNamingService();
        var handler = new GetFileNamingSettingsQueryHandler(service);

        var result = await handler.Handle(new GetFileNamingSettingsQuery(), CancellationToken.None);

        result.Should().NotBeNull();
        result.Settings.Should().NotBeNull();
        result.Presets.Should().NotBeEmpty();
        result.AvailableTokens.Should().NotBeEmpty();
    }

    [Fact]
    public void UpdateFileNamingSettingsCommandValidator_ShouldValidate()
    {
        var validator = new UpdateFileNamingSettingsCommandValidator();

        var invalid = validator.Validate(new UpdateFileNamingSettingsCommand(new FileNamingSettingsDto { Pattern = "" }));
        var valid = validator.Validate(new UpdateFileNamingSettingsCommand(new FileNamingSettingsDto { Pattern = "{Title}" }));

        invalid.IsValid.Should().BeFalse();
        valid.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task PreviewAndExecuteRenaming_ShouldDelegateToService()
    {
        var service = new FakeMediaNamingService();

        var previewHandler = new PreviewFileRenamingQueryHandler(service);
        var previewResult = await previewHandler.Handle(new PreviewFileRenamingQuery(new FileRenamePreviewRequest()), CancellationToken.None);

        previewResult.TotalVideos.Should().Be(5);
        previewResult.VideosToRename.Should().Be(3);

        var executeHandler = new ExecuteFileRenamingCommandHandler(service);
        var executeResult = await executeHandler.Handle(new ExecuteFileRenamingCommand(new FileRenameExecuteRequest()), CancellationToken.None);

        executeResult.RenamedCount.Should().Be(3);
    }

    [Fact]
    public async Task RecycleBinWorkflow_ShouldQueryAndEmpty()
    {
        var service = new FakeMediaNamingService();

        var getHandler = new GetRecycleBinSummaryQueryHandler(service);
        var summary = await getHandler.Handle(new GetRecycleBinSummaryQuery(), CancellationToken.None);

        summary.FileCount.Should().Be(4);

        var emptyHandler = new EmptyRecycleBinCommandHandler(service);
        var deletedCount = await emptyHandler.Handle(new EmptyRecycleBinCommand(), CancellationToken.None);

        deletedCount.Should().Be(4);
    }
}
