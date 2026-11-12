using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Radarr;

namespace MyTube.Backend.Application.Features.Radarr.Commands;

public record TestRadarrConnectionCommand(RadarrTestConnectionRequest Request) : IRequest<RadarrStatusDto>;

public class TestRadarrConnectionCommandValidator : AbstractValidator<TestRadarrConnectionCommand>
{
    public TestRadarrConnectionCommandValidator()
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

public class TestRadarrConnectionCommandHandler : IRequestHandler<TestRadarrConnectionCommand, RadarrStatusDto>
{
    private readonly IRadarrService _radarrService;

    public TestRadarrConnectionCommandHandler(IRadarrService radarrService)
    {
        _radarrService = radarrService;
    }

    public async Task<RadarrStatusDto> Handle(TestRadarrConnectionCommand request, CancellationToken cancellationToken)
    {
        var apiKey = request.Request.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Contains("****"))
        {
            apiKey = _radarrService.GetCurrentSettings().ApiKey;
        }

        return await _radarrService.TestConnectionAsync(request.Request.BaseUrl, apiKey, cancellationToken);
    }
}
