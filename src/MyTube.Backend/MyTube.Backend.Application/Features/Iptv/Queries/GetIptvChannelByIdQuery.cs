using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Iptv;

namespace MyTube.Backend.Application.Features.Iptv.Queries;

public record GetIptvChannelByIdQuery(string ChannelId, string? UserId) : IRequest<IptvChannelDto?>;

public class GetIptvChannelByIdQueryValidator : AbstractValidator<GetIptvChannelByIdQuery>
{
    public GetIptvChannelByIdQueryValidator()
    {
        RuleFor(x => x.ChannelId)
            .NotEmpty().WithMessage("ChannelId is required.");
    }
}

public class GetIptvChannelByIdQueryHandler : IRequestHandler<GetIptvChannelByIdQuery, IptvChannelDto?>
{
    private readonly IIptvService _iptvService;

    public GetIptvChannelByIdQueryHandler(IIptvService iptvService)
    {
        _iptvService = iptvService;
    }

    public async Task<IptvChannelDto?> Handle(GetIptvChannelByIdQuery request, CancellationToken cancellationToken)
    {
        return await _iptvService.GetChannelByIdAsync(request.ChannelId, request.UserId, cancellationToken);
    }
}

