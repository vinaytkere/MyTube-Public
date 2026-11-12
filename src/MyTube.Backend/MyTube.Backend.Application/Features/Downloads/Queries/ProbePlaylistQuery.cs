using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;

namespace MyTube.Backend.Application.Features.Downloads.Queries;

public record ProbePlaylistQuery(string Url) : IRequest<PlaylistProbeResultDto>;

public class ProbePlaylistQueryValidator : AbstractValidator<ProbePlaylistQuery>
{
    public ProbePlaylistQueryValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("Playlist URL is required.")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                         (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("A valid HTTP or HTTPS playlist URL is required.");
    }
}

public class ProbePlaylistQueryHandler : IRequestHandler<ProbePlaylistQuery, PlaylistProbeResultDto>
{
    private readonly IDownloadManager _downloadManager;

    public ProbePlaylistQueryHandler(IDownloadManager downloadManager)
    {
        _downloadManager = downloadManager;
    }

    public async Task<PlaylistProbeResultDto> Handle(ProbePlaylistQuery request, CancellationToken cancellationToken)
    {
        return await _downloadManager.ProbePlaylistAsync(request.Url, cancellationToken);
    }
}
