using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyTube.Backend.Application.FileNaming;

namespace MyTube.Backend.Application.Features.FileNaming.Queries;

public record GetRecycleBinSummaryQuery() : IRequest<RecycleBinSummaryDto>;

public class GetRecycleBinSummaryQueryHandler : IRequestHandler<GetRecycleBinSummaryQuery, RecycleBinSummaryDto>
{
    private readonly IMediaNamingService _namingService;

    public GetRecycleBinSummaryQueryHandler(IMediaNamingService namingService)
    {
        _namingService = namingService;
    }

    public async Task<RecycleBinSummaryDto> Handle(GetRecycleBinSummaryQuery request, CancellationToken cancellationToken)
    {
        return await _namingService.GetRecycleBinSummaryAsync(cancellationToken);
    }
}
