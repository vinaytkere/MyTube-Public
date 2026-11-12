using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyTube.Backend.Application.Radarr;

namespace MyTube.Backend.Application.Features.Radarr.Queries;

public record GetRadarrSettingsQuery : IRequest<RadarrSettingsResponseDto>;

public class GetRadarrSettingsQueryHandler : IRequestHandler<GetRadarrSettingsQuery, RadarrSettingsResponseDto>
{
    private readonly IRadarrService _radarrService;

    public GetRadarrSettingsQueryHandler(IRadarrService radarrService)
    {
        _radarrService = radarrService;
    }

    public Task<RadarrSettingsResponseDto> Handle(GetRadarrSettingsQuery request, CancellationToken cancellationToken)
    {
        var s = _radarrService.GetCurrentSettings();
        var maskedKey = string.IsNullOrEmpty(s.ApiKey) 
            ? string.Empty 
            : $"{s.ApiKey[..Math.Min(4, s.ApiKey.Length)]}****";

        var response = new RadarrSettingsResponseDto(
            s.BaseUrl,
            maskedKey,
            s.RootFolderPath,
            s.QualityProfileId,
            !string.IsNullOrWhiteSpace(s.ApiKey));

        return Task.FromResult(response);
    }
}
