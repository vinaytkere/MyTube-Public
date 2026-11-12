using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyTube.Backend.Domain;

namespace MyTube.Backend.Application.Features.AI.Queries;

public record GetAiQuotasQuery() : IRequest<List<AiModelQuota>>;

public class GetAiQuotasQueryHandler : IRequestHandler<GetAiQuotasQuery, List<AiModelQuota>>
{
    private readonly IAiQuotaManager _quotaManager;

    public GetAiQuotasQueryHandler(IAiQuotaManager quotaManager)
    {
        _quotaManager = quotaManager;
    }

    public async Task<List<AiModelQuota>> Handle(GetAiQuotasQuery request, CancellationToken cancellationToken)
    {
        return await _quotaManager.GetQuotasAsync();
    }
}
