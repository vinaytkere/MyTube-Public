using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Radarr;

namespace MyTube.Backend.Application.Features.Radarr.Queries;

public record GetRadarrTorrentDownloadQuery(int TransmissionId, int? FileIndex = null, int? MovieId = null) 
    : IRequest<RadarrTorrentDownloadResult?>;

public class GetRadarrTorrentDownloadQueryValidator : AbstractValidator<GetRadarrTorrentDownloadQuery>
{
    public GetRadarrTorrentDownloadQueryValidator()
    {
        RuleFor(x => x.TransmissionId)
            .GreaterThan(0).WithMessage("Valid transmission ID is required.");
    }
}

public class GetRadarrTorrentDownloadQueryHandler : IRequestHandler<GetRadarrTorrentDownloadQuery, RadarrTorrentDownloadResult?>
{
    private readonly IRadarrService _radarrService;

    public GetRadarrTorrentDownloadQueryHandler(IRadarrService radarrService)
    {
        _radarrService = radarrService;
    }

    public async Task<RadarrTorrentDownloadResult?> Handle(GetRadarrTorrentDownloadQuery request, CancellationToken cancellationToken)
    {
        var info = await _radarrService.GetTransmissionDownloadInfoAsync(
            request.TransmissionId, 
            request.FileIndex, 
            request.MovieId, 
            cancellationToken);

        if (info == null) return null;

        return new RadarrTorrentDownloadResult(
            info.Value.TargetPath,
            info.Value.IsDirectory,
            info.Value.Files,
            info.Value.DownloadName,
            info.Value.ErrorMessage);
    }
}
