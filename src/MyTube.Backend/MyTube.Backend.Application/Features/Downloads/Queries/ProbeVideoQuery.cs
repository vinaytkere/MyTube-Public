using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;

namespace MyTube.Backend.Application.Features.Downloads.Queries;

public record ProbeVideoQuery(string Url) : IRequest<VideoProbeResultDto>;

public class ProbeVideoQueryValidator : AbstractValidator<ProbeVideoQuery>
{
    public ProbeVideoQueryValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("URL is required.");
    }
}

public class ProbeVideoQueryHandler : IRequestHandler<ProbeVideoQuery, VideoProbeResultDto>
{
    private readonly IDownloadManager _downloadManager;

    public ProbeVideoQueryHandler(IDownloadManager downloadManager)
    {
        _downloadManager = downloadManager;
    }

    public async Task<VideoProbeResultDto> Handle(ProbeVideoQuery request, CancellationToken cancellationToken)
    {
        return await _downloadManager.ProbeVideoAsync(request.Url);
    }
}
