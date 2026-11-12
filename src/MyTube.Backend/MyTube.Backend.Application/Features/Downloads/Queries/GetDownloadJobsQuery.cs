using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace MyTube.Backend.Application.Features.Downloads.Queries;

public record GetDownloadJobsQuery() : IRequest<IReadOnlyList<DownloadJobDto>>;

public class GetDownloadJobsQueryHandler : IRequestHandler<GetDownloadJobsQuery, IReadOnlyList<DownloadJobDto>>
{
    private readonly IDownloadManager _downloadManager;

    public GetDownloadJobsQueryHandler(IDownloadManager downloadManager)
    {
        _downloadManager = downloadManager;
    }

    public Task<IReadOnlyList<DownloadJobDto>> Handle(GetDownloadJobsQuery request, CancellationToken cancellationToken)
    {
        var jobs = _downloadManager.GetJobs();
        return Task.FromResult<IReadOnlyList<DownloadJobDto>>(jobs);
    }
}
