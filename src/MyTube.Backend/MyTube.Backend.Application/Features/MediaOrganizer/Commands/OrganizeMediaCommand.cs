using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.MediaOrganizer;
using MyTube.Backend.Application.MediaOrganizer.Dtos;

namespace MyTube.Backend.Application.Features.MediaOrganizer.Commands;

public record OrganizeMediaCommand(Guid VideoId) : IRequest<MediaOrganizeResultDto>;

public class OrganizeMediaCommandValidator : AbstractValidator<OrganizeMediaCommand>
{
    public OrganizeMediaCommandValidator()
    {
        RuleFor(x => x.VideoId)
            .NotEmpty().WithMessage("VideoId must be provided.");
    }
}

public class OrganizeMediaCommandHandler : IRequestHandler<OrganizeMediaCommand, MediaOrganizeResultDto>
{
    private readonly IMediaOrganizerService _organizerService;

    public OrganizeMediaCommandHandler(IMediaOrganizerService organizerService)
    {
        _organizerService = organizerService;
    }

    public Task<MediaOrganizeResultDto> Handle(OrganizeMediaCommand request, CancellationToken cancellationToken)
    {
        return _organizerService.OrganizeMediaAsync(request.VideoId, cancellationToken);
    }
}
