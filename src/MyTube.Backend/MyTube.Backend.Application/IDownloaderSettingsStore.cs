using System.Threading;
using System.Threading.Tasks;

namespace MyTube.Backend.Application;

public interface IDownloaderSettingsStore
{
    DownloaderSettingsDto GetSettings();
    Task UpdateSettingsAsync(DownloaderSettingsDto settings, CancellationToken cancellationToken = default);
}
