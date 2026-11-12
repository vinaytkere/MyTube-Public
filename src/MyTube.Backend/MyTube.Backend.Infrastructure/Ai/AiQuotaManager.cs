using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application;
using MyTube.Backend.Domain;

namespace MyTube.Backend.Infrastructure.Ai;

public class AiQuotaManager : IAiQuotaManager
{
    private readonly VideoDbContext _db;
    private readonly ILogger<AiQuotaManager> _logger;

    public AiQuotaManager(VideoDbContext db, ILogger<AiQuotaManager> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<AiModelQuota?> GetAvailableModelAsync(string taskType, string? provider = null)
    {
        var query = _db.AiModelQuotas
            .Where(q => q.IsEnabled && q.Status != "ModelNotFound" && q.Status != "Disabled" && q.TaskType == taskType);

        if (!string.IsNullOrWhiteSpace(provider))
        {
            query = query.Where(q => q.Provider.ToLower() == provider.ToLower());
        }

        var quotas = await query
            .OrderBy(q => q.PriorityOrder)
            .ToListAsync();

        var now = DateTime.UtcNow;

        foreach (var quota in quotas)
        {
            EvaluateWindowReset(quota, now);

            // Check if model is within RPM, RPD, and Audio limits
            bool rpmOk = quota.RpmLimit == 0 || quota.CurrentMinuteRequests < quota.RpmLimit;
            bool rpdOk = quota.RpdLimit == 0 || quota.CurrentDayRequests < quota.RpdLimit;
            bool audioOk = quota.AudioSecondsPerDayLimit == 0 || quota.CurrentDayAudioSeconds < quota.AudioSecondsPerDayLimit;

            if (rpmOk && rpdOk && audioOk)
            {
                await _db.SaveChangesAsync();
                return quota;
            }
            else
            {
                _logger.LogWarning($"Model {quota.Provider} ({quota.ModelName}) RPM/RPD capacity reached. RPM: {quota.CurrentMinuteRequests}/{quota.RpmLimit}, RPD: {quota.CurrentDayRequests}/{quota.RpdLimit}. Bypassing to next model.");
            }
        }

        await _db.SaveChangesAsync();
        return null;
    }

    public async Task RecordRequestAsync(int quotaId, int audioSecondsUsed = 0)
    {
        var quota = await _db.AiModelQuotas.FindAsync(quotaId);
        if (quota != null)
        {
            var now = DateTime.UtcNow;
            EvaluateWindowReset(quota, now);

            quota.CurrentMinuteRequests++;
            quota.CurrentDayRequests++;
            if (audioSecondsUsed > 0)
            {
                quota.CurrentDayAudioSeconds += audioSecondsUsed;
            }
            if (quota.Status != "ModelNotFound" && quota.Status != "Disabled")
            {
                quota.Status = "Active";
            }
            quota.LastCheckedAt = now;
            await _db.SaveChangesAsync();
        }
    }

    public async Task RecordErrorAsync(int quotaId, int statusCode, string? errorMessage = null)
    {
        var quota = await _db.AiModelQuotas.FindAsync(quotaId);
        if (quota != null)
        {
            var now = DateTime.UtcNow;
            quota.LastCheckedAt = now;
            quota.LastErrorDetails = errorMessage ?? $"HTTP status code {statusCode}";

            // Map status codes explicitly without artificially maxing out request counters
            switch (statusCode)
            {
                case 401:
                    quota.Status = "AuthError";
                    _logger.LogWarning($"Authentication error (401) for model {quota.Provider} ({quota.ModelName}).");
                    break;
                case 403:
                    quota.Status = "PermissionError";
                    _logger.LogWarning($"Permission error (403) for model {quota.Provider} ({quota.ModelName}).");
                    break;
                case 404:
                    quota.Status = "ModelNotFound";
                    quota.IsEnabled = false; // Disable obsolete record, preserve history
                    _logger.LogWarning($"Model {quota.Provider} ({quota.ModelName}) returned HTTP 404 ModelNotFound. Flagged as disabled.");
                    break;
                case 429:
                    quota.Status = "RateLimited";
                    _logger.LogWarning($"Rate limit hit (429) for model {quota.Provider} ({quota.ModelName}).");
                    break;
                default:
                    if (statusCode >= 500)
                    {
                        quota.Status = "ProviderError";
                        _logger.LogWarning($"Provider server error ({statusCode}) for model {quota.Provider} ({quota.ModelName}).");
                    }
                    break;
            }

            await _db.SaveChangesAsync();
        }
    }

    public async Task<List<AiModelQuota>> GetQuotasAsync()
    {
        var quotas = await _db.AiModelQuotas.OrderBy(q => q.PriorityOrder).ToListAsync();
        var now = DateTime.UtcNow;
        bool changed = false;

        foreach (var q in quotas)
        {
            if (EvaluateWindowReset(q, now))
            {
                changed = true;
            }
        }

        if (changed)
        {
            await _db.SaveChangesAsync();
        }

        return quotas;
    }

    public async Task ResetQuotasAsync()
    {
        var quotas = await _db.AiModelQuotas.ToListAsync();
        var now = DateTime.UtcNow;
        foreach (var q in quotas)
        {
            q.CurrentMinuteRequests = 0;
            q.CurrentDayRequests = 0;
            q.CurrentDayAudioSeconds = 0;
            q.WindowStartMinute = now;
            q.WindowStartDay = now.Date;
            if (q.Status != "ModelNotFound")
            {
                q.IsEnabled = true;
                q.Status = "Active";
            }
            q.LastErrorDetails = null;
        }
        await _db.SaveChangesAsync();
        _logger.LogInformation("All AI Model Quota usage counters reset successfully.");
    }

    private static bool EvaluateWindowReset(AiModelQuota quota, DateTime now)
    {
        bool resetOccurred = false;

        // Reset minute counter if 1 minute has elapsed
        if ((now - quota.WindowStartMinute).TotalSeconds >= 60)
        {
            quota.CurrentMinuteRequests = 0;
            quota.WindowStartMinute = now;
            if (quota.Status == "RateLimited")
            {
                quota.Status = "Active";
            }
            resetOccurred = true;
        }

        // Reset day counter if calendar day or 24 hours passed
        if (now.Date > quota.WindowStartDay.Date || (now - quota.WindowStartDay).TotalHours >= 24)
        {
            quota.CurrentDayRequests = 0;
            quota.CurrentDayAudioSeconds = 0;
            quota.WindowStartDay = now.Date;
            resetOccurred = true;
        }

        return resetOccurred;
    }
}
