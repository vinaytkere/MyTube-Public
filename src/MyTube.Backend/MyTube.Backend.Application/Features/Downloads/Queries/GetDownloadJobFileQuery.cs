using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;

namespace MyTube.Backend.Application.Features.Downloads.Queries;

public record GetDownloadJobFileQuery(Guid JobId) : IRequest<DownloadJobFileResult?>;

public class GetDownloadJobFileQueryValidator : AbstractValidator<GetDownloadJobFileQuery>
{
    public GetDownloadJobFileQueryValidator()
    {
        RuleFor(x => x.JobId)
            .NotEmpty().WithMessage("Job ID cannot be empty.");
    }
}

public class GetDownloadJobFileQueryHandler : IRequestHandler<GetDownloadJobFileQuery, DownloadJobFileResult?>
{
    private readonly IDownloadManager _downloadManager;

    public GetDownloadJobFileQueryHandler(IDownloadManager downloadManager)
    {
        _downloadManager = downloadManager;
    }

    public Task<DownloadJobFileResult?> Handle(GetDownloadJobFileQuery request, CancellationToken cancellationToken)
    {
        var job = _downloadManager.GetJobs().FirstOrDefault(j => j.Id == request.JobId);
        if (job == null || string.IsNullOrWhiteSpace(job.FilePath) || !File.Exists(job.FilePath))
        {
            return Task.FromResult<DownloadJobFileResult?>(null);
        }

        var fullPath = Path.GetFullPath(job.FilePath);
        var fileName = Path.GetFileName(fullPath);
        var ext = Path.GetExtension(fullPath).ToLowerInvariant();

        var contentType = ext switch
        {
            ".mp3" => "audio/mpeg",
            ".m4a" => "audio/mp4",
            ".opus" => "audio/opus",
            ".wav" => "audio/wav",
            ".webm" => "video/webm",
            ".mkv" => "video/x-matroska",
            _ => "video/mp4"
        };

        return Task.FromResult<DownloadJobFileResult?>(new DownloadJobFileResult(fullPath, fileName, contentType));
    }
}
