using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Radarr;

namespace MyTube.Backend.Application.Features.Radarr.Commands;

public record AddRadarrMovieCommand(RadarrAddMovieRequest Request) : IRequest<RadarrMovieDto?>;

public class AddRadarrMovieCommandValidator : AbstractValidator<AddRadarrMovieCommand>
{
    public AddRadarrMovieCommandValidator()
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

public class AddRadarrMovieCommandHandler : IRequestHandler<AddRadarrMovieCommand, RadarrMovieDto?>
{
    private readonly IRadarrService _radarrService;

    public AddRadarrMovieCommandHandler(IRadarrService radarrService)
    {
        _radarrService = radarrService;
    }

    public async Task<RadarrMovieDto?> Handle(AddRadarrMovieCommand request, CancellationToken cancellationToken)
    {
        return await _radarrService.AddMovieAsync(request.Request, cancellationToken);
    }
}
