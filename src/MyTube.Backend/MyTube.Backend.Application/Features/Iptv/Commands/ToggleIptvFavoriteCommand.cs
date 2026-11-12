using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Iptv;

namespace MyTube.Backend.Application.Features.Iptv.Commands;

public record ToggleIptvFavoriteCommand(string UserId, string ChannelId) : IRequest<bool>;

public class ToggleIptvFavoriteCommandValidator : AbstractValidator<ToggleIptvFavoriteCommand>
{
    public ToggleIptvFavoriteCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");

        RuleFor(x => x.ChannelId)
            .NotEmpty().WithMessage("ChannelId is required.");
    }
}

public class ToggleIptvFavoriteCommandHandler : IRequestHandler<ToggleIptvFavoriteCommand, bool>
{
    private readonly IIptvService _iptvService;

    public ToggleIptvFavoriteCommandHandler(IIptvService iptvService)
    {
        _iptvService = iptvService;
    }

    public async Task<bool> Handle(ToggleIptvFavoriteCommand request, CancellationToken cancellationToken)
    {
        return await _iptvService.ToggleFavoriteAsync(request.UserId, request.ChannelId, cancellationToken);
    }
}

