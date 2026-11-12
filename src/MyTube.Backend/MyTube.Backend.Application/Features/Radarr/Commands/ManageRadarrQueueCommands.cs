using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Radarr;

namespace MyTube.Backend.Application.Features.Radarr.Commands;

public record CancelRadarrQueueItemCommand(int Id, int? TransmissionId = null, bool DeleteData = true) : IRequest<bool>;

public class CancelRadarrQueueItemCommandValidator : AbstractValidator<CancelRadarrQueueItemCommand>
{
    public CancelRadarrQueueItemCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => x.Id > 0 || (x.TransmissionId.HasValue && x.TransmissionId.Value > 0))
            .WithMessage("Valid queue item ID or transmission ID is required.");
    }
}

public class CancelRadarrQueueItemCommandHandler : IRequestHandler<CancelRadarrQueueItemCommand, bool>
{
    private readonly IRadarrService _radarrService;

    public CancelRadarrQueueItemCommandHandler(IRadarrService radarrService)
    {
        _radarrService = radarrService;
    }

    public async Task<bool> Handle(CancelRadarrQueueItemCommand request, CancellationToken cancellationToken)
    {
        return await _radarrService.CancelQueueItemAsync(request.Id, request.TransmissionId, request.DeleteData, cancellationToken);
    }
}

public record PauseRadarrQueueItemCommand(int Id, int? TransmissionId = null) : IRequest<bool>;

public class PauseRadarrQueueItemCommandValidator : AbstractValidator<PauseRadarrQueueItemCommand>
{
    public PauseRadarrQueueItemCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => x.Id > 0 || (x.TransmissionId.HasValue && x.TransmissionId.Value > 0))
            .WithMessage("Valid queue item ID or transmission ID is required.");
    }
}

public class PauseRadarrQueueItemCommandHandler : IRequestHandler<PauseRadarrQueueItemCommand, bool>
{
    private readonly IRadarrService _radarrService;

    public PauseRadarrQueueItemCommandHandler(IRadarrService radarrService)
    {
        _radarrService = radarrService;
    }

    public async Task<bool> Handle(PauseRadarrQueueItemCommand request, CancellationToken cancellationToken)
    {
        return await _radarrService.PauseQueueItemAsync(request.Id, request.TransmissionId, cancellationToken);
    }
}

public record ResumeRadarrQueueItemCommand(int Id, int? TransmissionId = null) : IRequest<bool>;

public class ResumeRadarrQueueItemCommandValidator : AbstractValidator<ResumeRadarrQueueItemCommand>
{
    public ResumeRadarrQueueItemCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => x.Id > 0 || (x.TransmissionId.HasValue && x.TransmissionId.Value > 0))
            .WithMessage("Valid queue item ID or transmission ID is required.");
    }
}

public class ResumeRadarrQueueItemCommandHandler : IRequestHandler<ResumeRadarrQueueItemCommand, bool>
{
    private readonly IRadarrService _radarrService;

    public ResumeRadarrQueueItemCommandHandler(IRadarrService radarrService)
    {
        _radarrService = radarrService;
    }

    public async Task<bool> Handle(ResumeRadarrQueueItemCommand request, CancellationToken cancellationToken)
    {
        return await _radarrService.ResumeQueueItemAsync(request.Id, request.TransmissionId, cancellationToken);
    }
}

public record KickstartRadarrQueueItemCommand(int Id, int? TransmissionId = null) : IRequest<bool>;

public class KickstartRadarrQueueItemCommandValidator : AbstractValidator<KickstartRadarrQueueItemCommand>
{
    public KickstartRadarrQueueItemCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => x.Id > 0 || (x.TransmissionId.HasValue && x.TransmissionId.Value > 0))
            .WithMessage("Valid queue item ID or transmission ID is required.");
    }
}

public class KickstartRadarrQueueItemCommandHandler : IRequestHandler<KickstartRadarrQueueItemCommand, bool>
{
    private readonly IRadarrService _radarrService;

    public KickstartRadarrQueueItemCommandHandler(IRadarrService radarrService)
    {
        _radarrService = radarrService;
    }

    public async Task<bool> Handle(KickstartRadarrQueueItemCommand request, CancellationToken cancellationToken)
    {
        return await _radarrService.KickstartQueueItemAsync(request.Id, request.TransmissionId, cancellationToken);
    }
}
