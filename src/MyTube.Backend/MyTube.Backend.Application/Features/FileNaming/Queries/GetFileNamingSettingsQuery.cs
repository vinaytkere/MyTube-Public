using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyTube.Backend.Application.FileNaming;

namespace MyTube.Backend.Application.Features.FileNaming.Queries;

public record GetFileNamingSettingsQuery() : IRequest<FileNamingConfigurationDto>;

public class GetFileNamingSettingsQueryHandler : IRequestHandler<GetFileNamingSettingsQuery, FileNamingConfigurationDto>
{
    private readonly IMediaNamingService _namingService;

    public GetFileNamingSettingsQueryHandler(IMediaNamingService namingService)
    {
        _namingService = namingService;
    }

    public Task<FileNamingConfigurationDto> Handle(GetFileNamingSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = _namingService.GetSettings();
        var presets = new List<FileNamingPresetDto>
        {
            new() { Id = "standard", Name = "Sonarr Standard", Pattern = "{Title} [{Quality}] [{VideoCodec}]", Description = "Clean title followed by quality and codec in brackets" },
            new() { Id = "organized", Name = "Organized Folder", Pattern = "{Category}/{Title} ({Year}) [{Quality}]", Description = "Groups into category folders with release year" },
            new() { Id = "scene", Name = "Scene Release", Pattern = "{CleanTitle}.{Year}.{Quality}.{VideoCodec}", Description = "Dot-separated format favored by release groups" },
            new() { Id = "detailed", Name = "Detailed Technical", Pattern = "{Title} - {Date} [{Resolution}] [{VideoCodec} {AudioCodec}]", Description = "Includes precise pixel resolution and audio codec" },
            new() { Id = "simple", Name = "Simple Title & Year", Pattern = "{Title} ({Year})", Description = "Minimalist naming" }
        };

        var availableTokens = new List<FileNamingTokenDto>
        {
            new() { Token = "{Title}", Description = "Display title of the video" },
            new() { Token = "{CleanTitle}", Description = "Title cleaned of special characters and dots" },
            new() { Token = "{OriginalFilename}", Description = "Original filename on disk without extension" },
            new() { Token = "{Category}", Description = "Category or playlist folder name" },
            new() { Token = "{Year}", Description = "Year the video was released or created" },
            new() { Token = "{Date}", Description = "Date formatted as YYYY-MM-DD" },
            new() { Token = "{Quality}", Description = "Quality label (e.g. 1080p, 4K, 720p, SD)" },
            new() { Token = "{Resolution}", Description = "Resolution dimensions (e.g. 1920x1080)" },
            new() { Token = "{VideoCodec}", Description = "Video codec (e.g. h264, hevc, av1)" },
            new() { Token = "{AudioCodec}", Description = "Audio codec (e.g. aac, mp3, opus)" }
        };

        return Task.FromResult(new FileNamingConfigurationDto
        {
            Settings = settings,
            Presets = presets,
            AvailableTokens = availableTokens
        });
    }
}
