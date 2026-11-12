using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Sonarr;

namespace MyTube.Backend.Application.Features.Sonarr.Commands;

public record TestSonarrConnectionCommand(SonarrTestConnectionRequest Request) : IRequest<SonarrStatusDto>;

public class TestSonarrConnectionCommandValidator : AbstractValidator<TestSonarrConnectionCommand>
{
    public TestSonarrConnectionCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull().WithMessage("Connection request cannot be null.");

        When(x => x.Request != null, () =>
        {
            RuleFor(x => x.Request.BaseUrl)
                .NotEmpty().WithMessage("Base URL is required.");
        });
    }
}

public class TestSonarrConnectionCommandHandler : IRequestHandler<TestSonarrConnectionCommand, SonarrStatusDto>
{
    private readonly ISonarrService _sonarrService;

    public TestSonarrConnectionCommandHandler(ISonarrService sonarrService)
    {
        _sonarrService = sonarrService;
    }

    public async Task<SonarrStatusDto> Handle(TestSonarrConnectionCommand request, CancellationToken cancellationToken)
    {
        var apiKey = request.Request.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Contains("****"))
        {
            apiKey = _sonarrService.GetCurrentSettings().ApiKey;
        }

        return await _sonarrService.TestConnectionAsync(request.Request.BaseUrl, apiKey, cancellationToken);
    }
}
