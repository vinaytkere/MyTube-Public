using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace MyTube.Backend.Application.Features.AI.Commands;

public record AnalyzeVideoCommand(Guid VideoId) : IRequest<bool>;

public class AnalyzeVideoCommandValidator : AbstractValidator<AnalyzeVideoCommand>
{
    public AnalyzeVideoCommandValidator()
    {
        RuleFor(x => x.VideoId)
            .NotEmpty().WithMessage("Video ID cannot be empty.");
    }
}

public class AnalyzeVideoCommandHandler : IRequestHandler<AnalyzeVideoCommand, bool>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public AnalyzeVideoCommandHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public Task<bool> Handle(AnalyzeVideoCommand request, CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IAiOrchestrator>();
            await orchestrator.AnalyzeVideoAsync(request.VideoId);
        });

        return Task.FromResult(true);
    }
}
