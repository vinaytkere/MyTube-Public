using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyTube.Backend.Application.Sonarr;

namespace MyTube.Backend.Application.Features.Sonarr.Queries;

public record GetSonarrSettingsQuery : IRequest<SonarrSettingsResponseDto>;

public class GetSonarrSettingsQueryHandler : IRequestHandler<GetSonarrSettingsQuery, SonarrSettingsResponseDto>
{
    private readonly ISonarrService _sonarrService;

    public GetSonarrSettingsQueryHandler(ISonarrService sonarrService)
    {
        _sonarrService = sonarrService;
    }

    public Task<SonarrSettingsResponseDto> Handle(GetSonarrSettingsQuery request, CancellationToken cancellationToken)
    {
        var s = _sonarrService.GetCurrentSettings();
        var maskedKey = string.IsNullOrEmpty(s.ApiKey)
            ? string.Empty
            : $"{s.ApiKey[..Math.Min(4, s.ApiKey.Length)]}****";

        var response = new SonarrSettingsResponseDto(
            s.BaseUrl,
            maskedKey,
            s.RootFolderPath,
            s.QualityProfileId,
            !string.IsNullOrWhiteSpace(s.ApiKey));

        return Task.FromResult(response);
    }
}
