using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;

namespace MyTube.Backend.Application.Features.Downloads.Queries;

public record GetAvailableQualitiesQuery(string Url) : IRequest<List<string>>;

public class GetAvailableQualitiesQueryValidator : AbstractValidator<GetAvailableQualitiesQuery>
{
    public GetAvailableQualitiesQueryValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("URL is required.");
    }
}

public class GetAvailableQualitiesQueryHandler : IRequestHandler<GetAvailableQualitiesQuery, List<string>>
{
    private readonly IDownloadManager _downloadManager;

    public GetAvailableQualitiesQueryHandler(IDownloadManager downloadManager)
    {
        _downloadManager = downloadManager;
    }

    public async Task<List<string>> Handle(GetAvailableQualitiesQuery request, CancellationToken cancellationToken)
    {
        return await _downloadManager.GetAvailableQualitiesAsync(request.Url);
    }
}
