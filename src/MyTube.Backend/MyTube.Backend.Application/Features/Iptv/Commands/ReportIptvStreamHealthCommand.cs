using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Iptv;

namespace MyTube.Backend.Application.Features.Iptv.Commands;

public record ReportIptvStreamHealthCommand(int StreamId, bool IsWorking, string? ErrorMessage = null) : IRequest<bool>;

public class ReportIptvStreamHealthCommandValidator : AbstractValidator<ReportIptvStreamHealthCommand>
{
    public ReportIptvStreamHealthCommandValidator()
    {
        RuleFor(x => x.StreamId)
            .GreaterThan(0).WithMessage("StreamId must be a positive integer.");
    }
}

public class ReportIptvStreamHealthCommandHandler : IRequestHandler<ReportIptvStreamHealthCommand, bool>
{
    private readonly IIptvService _iptvService;

    public ReportIptvStreamHealthCommandHandler(IIptvService iptvService)
    {
        _iptvService = iptvService;
    }

    public async Task<bool> Handle(ReportIptvStreamHealthCommand request, CancellationToken cancellationToken)
    {
        return await _iptvService.ReportStreamHealthAsync(request.StreamId, request.IsWorking, request.ErrorMessage, cancellationToken);
    }
}

