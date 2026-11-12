using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Iptv;

namespace MyTube.Backend.Application.Features.Iptv.Commands;

public record RecordIptvWatchCommand(string UserId, string ChannelId, int? StreamId = null) : IRequest;

public class RecordIptvWatchCommandValidator : AbstractValidator<RecordIptvWatchCommand>
{
    public RecordIptvWatchCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");

        RuleFor(x => x.ChannelId)
            .NotEmpty().WithMessage("ChannelId is required.");
    }
}

public class RecordIptvWatchCommandHandler : IRequestHandler<RecordIptvWatchCommand>
{
    private readonly IIptvService _iptvService;

    public RecordIptvWatchCommandHandler(IIptvService iptvService)
    {
        _iptvService = iptvService;
    }

    public async Task Handle(RecordIptvWatchCommand request, CancellationToken cancellationToken)
    {
        await _iptvService.RecordWatchHistoryAsync(request.UserId, request.ChannelId, request.StreamId, cancellationToken);
    }
}

