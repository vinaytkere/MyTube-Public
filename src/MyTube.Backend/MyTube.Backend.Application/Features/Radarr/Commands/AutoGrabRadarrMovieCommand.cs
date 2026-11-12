using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Radarr;

namespace MyTube.Backend.Application.Features.Radarr.Commands;

public record AutoGrabRadarrMovieCommand(RadarrAddMovieRequest Request) : IRequest<RadarrAutoGrabResultDto>;

public class AutoGrabRadarrMovieCommandValidator : AbstractValidator<AutoGrabRadarrMovieCommand>
{
    public AutoGrabRadarrMovieCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request body is required.");

        When(x => x.Request != null, () =>
        {
            RuleFor(x => x.Request.TmdbId)
                .GreaterThan(0).WithMessage("TmdbId is required.");

            RuleFor(x => x.Request.Title)
                .NotEmpty().WithMessage("Title is required.");
        });
    }
}

public class AutoGrabRadarrMovieCommandHandler : IRequestHandler<AutoGrabRadarrMovieCommand, RadarrAutoGrabResultDto>
{
    private readonly IRadarrService _radarrService;

    public AutoGrabRadarrMovieCommandHandler(IRadarrService radarrService)
    {
        _radarrService = radarrService;
    }

    public async Task<RadarrAutoGrabResultDto> Handle(AutoGrabRadarrMovieCommand request, CancellationToken cancellationToken)
    {
        return await _radarrService.AutoGrabMovieAsync(request.Request, cancellationToken);
    }
}
