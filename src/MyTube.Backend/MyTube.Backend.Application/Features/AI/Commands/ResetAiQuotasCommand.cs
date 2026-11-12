using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace MyTube.Backend.Application.Features.AI.Commands;

public record ResetAiQuotasCommand() : IRequest<Unit>;

public class ResetAiQuotasCommandHandler : IRequestHandler<ResetAiQuotasCommand, Unit>
{
    private readonly IAiQuotaManager _quotaManager;

    public ResetAiQuotasCommandHandler(IAiQuotaManager quotaManager)
    {
        _quotaManager = quotaManager;
    }

    public async Task<Unit> Handle(ResetAiQuotasCommand request, CancellationToken cancellationToken)
    {
        await _quotaManager.ResetQuotasAsync();
        return Unit.Value;
    }
}
