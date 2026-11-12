using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace MyTube.Backend.Application.Features.Downloads.Queries;

public record GetDownloaderSettingsQuery() : IRequest<DownloaderSettingsDto>;

public class GetDownloaderSettingsQueryHandler : IRequestHandler<GetDownloaderSettingsQuery, DownloaderSettingsDto>
{
    private readonly IDownloaderSettingsStore _settingsStore;

    public GetDownloaderSettingsQueryHandler(IDownloaderSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
    }

    public Task<DownloaderSettingsDto> Handle(GetDownloaderSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = _settingsStore.GetSettings();
        return Task.FromResult(settings);
    }
}
