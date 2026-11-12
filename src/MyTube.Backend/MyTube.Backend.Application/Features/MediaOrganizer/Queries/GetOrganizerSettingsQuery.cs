using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyTube.Backend.Application.MediaOrganizer;
using MyTube.Backend.Application.MediaOrganizer.Dtos;

namespace MyTube.Backend.Application.Features.MediaOrganizer.Queries;

public record GetOrganizerSettingsQuery : IRequest<MediaOrganizerSettingsDto>;

public class GetOrganizerSettingsQueryHandler : IRequestHandler<GetOrganizerSettingsQuery, MediaOrganizerSettingsDto>
{
    private readonly IMediaOrganizerService _organizerService;

    public GetOrganizerSettingsQueryHandler(IMediaOrganizerService organizerService)
    {
        _organizerService = organizerService;
    }

    public Task<MediaOrganizerSettingsDto> Handle(GetOrganizerSettingsQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_organizerService.GetSettings());
    }
}
