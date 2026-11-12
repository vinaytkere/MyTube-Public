using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Radarr;

namespace MyTube.Backend.Application.Features.Radarr.Commands;

public record DeleteRadarrMovieFilesCommand(int MovieId) : IRequest<bool>;

public class DeleteRadarrMovieFilesCommandValidator : AbstractValidator<DeleteRadarrMovieFilesCommand>
{
    public DeleteRadarrMovieFilesCommandValidator()
    {
        RuleFor(x => x.MovieId).GreaterThan(0).WithMessage("Valid movieId is required.");
    }
}

public class DeleteRadarrMovieFilesCommandHandler : IRequestHandler<DeleteRadarrMovieFilesCommand, bool>
{
    private readonly IRadarrService _radarrService;

    public DeleteRadarrMovieFilesCommandHandler(IRadarrService radarrService)
    {
        _radarrService = radarrService;
    }

    public async Task<bool> Handle(DeleteRadarrMovieFilesCommand request, CancellationToken cancellationToken)
    {
        return await _radarrService.DeleteMovieFilesAsync(request.MovieId, cancellationToken);
    }
}

public record BatchDeleteRadarrFilesCommand(List<int>? MovieIds, List<string>? FilePaths) : IRequest<int>;

public class BatchDeleteRadarrFilesCommandValidator : AbstractValidator<BatchDeleteRadarrFilesCommand>
{
    public BatchDeleteRadarrFilesCommandValidator()
    {
        RuleFor(x => x)
            .Must(cmd => (cmd.MovieIds != null && cmd.MovieIds.Count > 0) || (cmd.FilePaths != null && cmd.FilePaths.Count > 0))
            .WithMessage("No movie IDs or file paths provided for deletion.");
    }
}

public class BatchDeleteRadarrFilesCommandHandler : IRequestHandler<BatchDeleteRadarrFilesCommand, int>
{
    private readonly IRadarrService _radarrService;

    public BatchDeleteRadarrFilesCommandHandler(IRadarrService radarrService)
    {
        _radarrService = radarrService;
    }

    public async Task<int> Handle(BatchDeleteRadarrFilesCommand request, CancellationToken cancellationToken)
    {
        return await _radarrService.BatchDeleteMovieFilesAsync(
            request.MovieIds ?? new List<int>(),
            request.FilePaths,
            cancellationToken);
    }
}

public record DeleteAllRadarrDownloadsCommand : IRequest<int>;

public class DeleteAllRadarrDownloadsCommandHandler : IRequestHandler<DeleteAllRadarrDownloadsCommand, int>
{
    private readonly IRadarrService _radarrService;

    public DeleteAllRadarrDownloadsCommandHandler(IRadarrService radarrService)
    {
        _radarrService = radarrService;
    }

    public async Task<int> Handle(DeleteAllRadarrDownloadsCommand request, CancellationToken cancellationToken)
    {
        return await _radarrService.DeleteAllDownloadedFilesAsync(cancellationToken);
    }
}
