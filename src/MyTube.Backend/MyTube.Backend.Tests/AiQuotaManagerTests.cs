using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MyTube.Backend.Domain;
using MyTube.Backend.Infrastructure;
using MyTube.Backend.Infrastructure.Ai;
using Xunit;

namespace MyTube.Backend.Tests;

public class AiQuotaManagerTests
{
    private VideoDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<VideoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var db = new VideoDbContext(options);

        db.AiModelQuotas.AddRange(
            new AiModelQuota { Id = 1, Provider = "Groq", ModelName = "whisper-large-v3-turbo", RpmLimit = 20, RpdLimit = 2000, TaskType = "STT", PriorityOrder = 1, Status = "Active", IsEnabled = true, WindowStartMinute = DateTime.UtcNow, WindowStartDay = DateTime.UtcNow.Date },
            new AiModelQuota { Id = 2, Provider = "Groq", ModelName = "llama-3.3-70b-versatile", RpmLimit = 30, RpdLimit = 1000, TaskType = "Text", PriorityOrder = 1, Status = "Active", IsEnabled = true, WindowStartMinute = DateTime.UtcNow, WindowStartDay = DateTime.UtcNow.Date },
            new AiModelQuota { Id = 3, Provider = "Gemini", ModelName = "gemini-2.0-flash", RpmLimit = 15, RpdLimit = 1500, TaskType = "Text", PriorityOrder = 2, Status = "Active", IsEnabled = true, WindowStartMinute = DateTime.UtcNow, WindowStartDay = DateTime.UtcNow.Date }
        );
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task GetAvailableModelAsync_ShouldReturnHighestPriorityActiveModel()
    {
        var db = GetInMemoryDbContext();
        var manager = new AiQuotaManager(db, NullLogger<AiQuotaManager>.Instance);

        var model = await manager.GetAvailableModelAsync("Text");

        Assert.NotNull(model);
        Assert.Equal("Groq", model.Provider);
        Assert.Equal("llama-3.3-70b-versatile", model.ModelName);
    }

    [Fact]
    public async Task RecordRequestAsync_ShouldIncrementCounters()
    {
        var db = GetInMemoryDbContext();
        var manager = new AiQuotaManager(db, NullLogger<AiQuotaManager>.Instance);

        await manager.RecordRequestAsync(1, 120);

        var updated = await db.AiModelQuotas.FindAsync(1);
        Assert.NotNull(updated);
        Assert.Equal(1, updated.CurrentMinuteRequests);
        Assert.Equal(1, updated.CurrentDayRequests);
        Assert.Equal(120, updated.CurrentDayAudioSeconds);
    }

    [Fact]
    public async Task RecordErrorAsync_404_ShouldMarkModelNotFoundAndDisableRowWithoutDistortingCounters()
    {
        var db = GetInMemoryDbContext();
        var manager = new AiQuotaManager(db, NullLogger<AiQuotaManager>.Instance);

        await manager.RecordErrorAsync(2, 404, "Model not found on Groq API");

        var updated = await db.AiModelQuotas.FindAsync(2);
        Assert.NotNull(updated);
        Assert.Equal("ModelNotFound", updated.Status);
        Assert.False(updated.IsEnabled);
        Assert.Equal(0, updated.CurrentMinuteRequests); // Should NOT artificially set to 30!

        // Next fetch should fallback to Gemini (Priority #2) because model #2 is ModelNotFound & disabled
        var fallbackModel = await manager.GetAvailableModelAsync("Text");
        Assert.NotNull(fallbackModel);
        Assert.Equal("Gemini", fallbackModel.Provider);
        Assert.Equal("gemini-2.0-flash", fallbackModel.ModelName);
    }

    [Fact]
    public async Task RecordErrorAsync_429_ShouldSetRateLimitedWithoutArtificiallyMaxingCounters()
    {
        var db = GetInMemoryDbContext();
        var manager = new AiQuotaManager(db, NullLogger<AiQuotaManager>.Instance);

        await manager.RecordErrorAsync(2, 429, "Rate limit exceeded");

        var updated = await db.AiModelQuotas.FindAsync(2);
        Assert.NotNull(updated);
        Assert.Equal("RateLimited", updated.Status);
        Assert.Equal(0, updated.CurrentMinuteRequests); // Counter remains accurate local count
    }

    [Fact]
    public async Task RecordErrorAsync_StatusMapping_ShouldSetExactStatuses()
    {
        var db = GetInMemoryDbContext();
        var manager = new AiQuotaManager(db, NullLogger<AiQuotaManager>.Instance);

        await manager.RecordErrorAsync(1, 401, "Invalid API key");
        var q1 = await db.AiModelQuotas.FindAsync(1);
        Assert.Equal("AuthError", q1!.Status);

        await manager.RecordErrorAsync(2, 403, "Access forbidden");
        var q2 = await db.AiModelQuotas.FindAsync(2);
        Assert.Equal("PermissionError", q2!.Status);

        await manager.RecordErrorAsync(3, 503, "Service unavailable");
        var q3 = await db.AiModelQuotas.FindAsync(3);
        Assert.Equal("ProviderError", q3!.Status);
    }

    [Fact]
    public async Task GetQuotasAsync_ShouldResetExpiredWindowsOnRead()
    {
        var db = GetInMemoryDbContext();
        var manager = new AiQuotaManager(db, NullLogger<AiQuotaManager>.Instance);

        var q2 = await db.AiModelQuotas.FindAsync(2);
        q2!.CurrentMinuteRequests = 10;
        q2.WindowStartMinute = DateTime.UtcNow.AddMinutes(-2); // 2 minutes ago
        await db.SaveChangesAsync();

        var quotas = await manager.GetQuotasAsync();
        var updatedQ2 = quotas.First(q => q.Id == 2);

        Assert.Equal(0, updatedQ2.CurrentMinuteRequests); // Automatically reset on read!
    }

    [Fact]
    public async Task ResetQuotasAsync_ShouldResetAllCounters()
    {
        var db = GetInMemoryDbContext();
        var manager = new AiQuotaManager(db, NullLogger<AiQuotaManager>.Instance);

        await manager.RecordRequestAsync(1, 60);
        await manager.RecordErrorAsync(2, 429, "Rate limit");

        await manager.ResetQuotasAsync();

        var q1 = await db.AiModelQuotas.FindAsync(1);
        var q2 = await db.AiModelQuotas.FindAsync(2);

        Assert.Equal(0, q1!.CurrentMinuteRequests);
        Assert.Equal(0, q1!.CurrentDayRequests);
        Assert.Equal(0, q1!.CurrentDayAudioSeconds);
        Assert.Equal(0, q2!.CurrentMinuteRequests);
        Assert.Equal("Active", q2!.Status);
    }
}
