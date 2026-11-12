using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyTube.Backend.Application.Iptv;

namespace MyTube.Backend.Application.Features.Iptv.Queries;

public record GetIptvFiltersQuery : IRequest<IptvFilterOptionsDto>;

public class GetIptvFiltersQueryHandler : IRequestHandler<GetIptvFiltersQuery, IptvFilterOptionsDto>
{
    private readonly IIptvService _iptvService;

    public GetIptvFiltersQueryHandler(IIptvService iptvService)
    {
        _iptvService = iptvService;
    }

    public async Task<IptvFilterOptionsDto> Handle(GetIptvFiltersQuery request, CancellationToken cancellationToken)
    {
        return await _iptvService.GetFilterOptionsAsync(cancellationToken);
    }
}

