using System.Threading.Tasks;

namespace MyTube.Backend.Application;

public interface IAiModelHealthService
{
    Task ValidateAndSyncProviderModelsAsync(string provider);
    Task HandleModelNotFoundAsync(int quotaId, string modelName);
}
