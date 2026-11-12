using System.Collections.Generic;
using System.Threading.Tasks;
using MyTube.Backend.Domain;

namespace MyTube.Backend.Application;

public interface IAiQuotaManager
{
    Task<AiModelQuota?> GetAvailableModelAsync(string taskType, string? provider = null);
    Task RecordRequestAsync(int quotaId, int audioSecondsUsed = 0);
    Task RecordErrorAsync(int quotaId, int statusCode, string? errorMessage = null);
    Task<List<AiModelQuota>> GetQuotasAsync();
    Task ResetQuotasAsync();
}
