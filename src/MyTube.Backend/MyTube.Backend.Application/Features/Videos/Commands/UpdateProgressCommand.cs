using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Repositories;

namespace MyTube.Backend.Application.Features.Videos.Commands;

public record UpdateProgressCommand(
    string UserId,
    Guid VideoId,
    double PositionSeconds,
    double DurationSeconds) : IRequest<Unit>;

public class UpdateProgressCommandValidator : AbstractValidator<UpdateProgressCommand>
{
    public UpdateProgressCommandValidator()
    {
        RuleFor(x => x.VideoId)
            .NotEmpty().WithMessage("Video ID cannot be empty.");

        RuleFor(x => x.PositionSeconds)
            .GreaterThanOrEqualTo(0).WithMessage("Position must be greater than or equal to 0.");

        RuleFor(x => x.DurationSeconds)
            .GreaterThanOrEqualTo(0).WithMessage("Duration must be greater than or equal to 0.");
    }
}

public class UpdateProgressCommandHandler : IRequestHandler<UpdateProgressCommand, Unit>
{
    private readonly IVideoRepository _videoRepository;

    public UpdateProgressCommandHandler(IVideoRepository videoRepository)
    {
        _videoRepository = videoRepository;
    }

    public async Task<Unit> Handle(UpdateProgressCommand request, CancellationToken cancellationToken)
    {
        await _videoRepository.SaveUserProgressAsync(
            request.UserId,
            request.VideoId,
            request.PositionSeconds,
            request.DurationSeconds,
            cancellationToken);

        return Unit.Value;
    }
}
