using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyTube.Backend.Domain;

namespace MyTube.Backend.Application.Features.AI.Commands;

public record ValidateAiQuotasCommand() : IRequest<List<AiModelQuota>>;

public class ValidateAiQuotasCommandHandler : IRequestHandler<ValidateAiQuotasCommand, List<AiModelQuota>>
{
    private readonly IAiModelHealthService _healthService;
    private readonly IAiQuotaManager _quotaManager;

    public ValidateAiQuotasCommandHandler(
        IAiModelHealthService healthService,
        IAiQuotaManager quotaManager)
    {
        _healthService = healthService;
        _quotaManager = quotaManager;
    }

    public async Task<List<AiModelQuota>> Handle(ValidateAiQuotasCommand request, CancellationToken cancellationToken)
    {
        await _healthService.ValidateAndSyncProviderModelsAsync("Groq");
        await _healthService.ValidateAndSyncProviderModelsAsync("Gemini");
        return await _quotaManager.GetQuotasAsync();
    }
}
