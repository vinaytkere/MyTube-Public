using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Sonarr;

namespace MyTube.Backend.Application.Features.Sonarr.Commands;

public record AddSonarrSeriesCommand(SonarrAddSeriesRequest Request) : IRequest<SonarrSeriesDto?>;

public class AddSonarrSeriesCommandValidator : AbstractValidator<AddSonarrSeriesCommand>
{
    public AddSonarrSeriesCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request body is required.");

        When(x => x.Request != null, () =>
        {
            RuleFor(x => x.Request.TvdbId)
                .GreaterThan(0).WithMessage("TvdbId is required.");

            RuleFor(x => x.Request.Title)
                .NotEmpty().WithMessage("Title is required.");
        });
    }
}

public class AddSonarrSeriesCommandHandler : IRequestHandler<AddSonarrSeriesCommand, SonarrSeriesDto?>
{
    private readonly ISonarrService _sonarrService;

    public AddSonarrSeriesCommandHandler(ISonarrService sonarrService)
    {
        _sonarrService = sonarrService;
    }

    public async Task<SonarrSeriesDto?> Handle(AddSonarrSeriesCommand request, CancellationToken cancellationToken)
    {
        return await _sonarrService.AddSeriesAsync(request.Request, cancellationToken);
    }
}
