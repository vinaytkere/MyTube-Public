using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Radarr;

namespace MyTube.Backend.Application.Features.Radarr.Queries;

public record GetRadarrTargetDownloadQuery(string Path) : IRequest<RadarrTargetDownloadResult?>;

public class GetRadarrTargetDownloadQueryValidator : AbstractValidator<GetRadarrTargetDownloadQuery>
{
    public GetRadarrTargetDownloadQueryValidator()
    {
        RuleFor(x => x.Path)
            .NotEmpty().WithMessage("File path is required.");
    }
}

public class GetRadarrTargetDownloadQueryHandler : IRequestHandler<GetRadarrTargetDownloadQuery, RadarrTargetDownloadResult?>
{
    private readonly IRadarrService _radarrService;

    public GetRadarrTargetDownloadQueryHandler(IRadarrService radarrService)
    {
        _radarrService = radarrService;
    }

    public async Task<RadarrTargetDownloadResult?> Handle(GetRadarrTargetDownloadQuery request, CancellationToken cancellationToken)
    {
        var target = await _radarrService.GetDownloadTargetAsync(request.Path, cancellationToken);
        if (target == null) return null;

        return new RadarrTargetDownloadResult(target.Value.ResolvedPath, target.Value.IsDirectory, target.Value.ContentType, target.Value.FileName);
    }
}
