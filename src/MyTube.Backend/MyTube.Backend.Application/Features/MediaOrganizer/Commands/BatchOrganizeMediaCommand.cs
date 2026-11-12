using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyTube.Backend.Application.MediaOrganizer;
using MyTube.Backend.Application.MediaOrganizer.Dtos;

namespace MyTube.Backend.Application.Features.MediaOrganizer.Commands;

public record BatchOrganizeMediaCommand(List<Guid>? VideoIds = null) : IRequest<List<MediaOrganizeResultDto>>;

public class BatchOrganizeMediaCommandHandler : IRequestHandler<BatchOrganizeMediaCommand, List<MediaOrganizeResultDto>>
{
    private readonly IMediaOrganizerService _organizerService;

    public BatchOrganizeMediaCommandHandler(IMediaOrganizerService organizerService)
    {
        _organizerService = organizerService;
    }

    public Task<List<MediaOrganizeResultDto>> Handle(BatchOrganizeMediaCommand request, CancellationToken cancellationToken)
    {
        return _organizerService.BatchOrganizeAsync(request.VideoIds, cancellationToken);
    }
}
