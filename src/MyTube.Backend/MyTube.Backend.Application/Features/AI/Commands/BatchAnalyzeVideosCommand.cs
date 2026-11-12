using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace MyTube.Backend.Application.Features.AI.Commands;

public record BatchAnalyzeVideosCommand(List<Guid> VideoIds) : IRequest<int>;

public class BatchAnalyzeVideosCommandValidator : AbstractValidator<BatchAnalyzeVideosCommand>
{
    public BatchAnalyzeVideosCommandValidator()
    {
        RuleFor(x => x.VideoIds)
            .NotEmpty().WithMessage("No video IDs provided.");
    }
}

public class BatchAnalyzeVideosCommandHandler : IRequestHandler<BatchAnalyzeVideosCommand, int>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public BatchAnalyzeVideosCommandHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public Task<int> Handle(BatchAnalyzeVideosCommand request, CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IAiOrchestrator>();
            await orchestrator.BatchAnalyzeVideosAsync(request.VideoIds);
        });

        return Task.FromResult(request.VideoIds.Count);
    }
}
