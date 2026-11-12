using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MyTube.Backend.Application;
using MyTube.Backend.Application.Features.AI.Commands;
using MyTube.Backend.Application.Features.AI.Queries;
using MyTube.Backend.Domain;
using Xunit;

namespace MyTube.Backend.Tests;

public class CqrsAiTests
{
    private class FakeQuotaManager : IAiQuotaManager
    {
        private readonly List<AiModelQuota> _quotas = new()
        {
            new AiModelQuota { Id = 1, Provider = "Cerebras", ModelName = "llama-3.3-70b", IsEnabled = true },
            new AiModelQuota { Id = 2, Provider = "Groq", ModelName = "llama-3.3-70b-versatile", IsEnabled = true }
        };

        public bool ResetCalled { get; private set; }

        public Task<AiModelQuota?> GetAvailableModelAsync(string taskType, string? provider = null) => Task.FromResult<AiModelQuota?>(_quotas[0]);
        public Task RecordRequestAsync(int quotaId, int audioSecondsUsed = 0) => Task.CompletedTask;
        public Task RecordErrorAsync(int quotaId, int statusCode, string? errorMessage = null) => Task.CompletedTask;
        public Task<List<AiModelQuota>> GetQuotasAsync() => Task.FromResult(_quotas);
        public Task ResetQuotasAsync()
        {
            ResetCalled = true;
            return Task.CompletedTask;
        }
    }

    private class FakeHealthService : IAiModelHealthService
    {
        public List<string> SyncedProviders { get; } = new();

        public Task ValidateAndSyncProviderModelsAsync(string provider)
        {
            SyncedProviders.Add(provider);
            return Task.CompletedTask;
        }

        public Task HandleModelNotFoundAsync(int quotaId, string modelName) => Task.CompletedTask;
    }

    [Fact]
    public void AnalyzeVideoCommandValidator_ShouldValidateGuid()
    {
        var validator = new AnalyzeVideoCommandValidator();

        var invalid = validator.Validate(new AnalyzeVideoCommand(Guid.Empty));
        var valid = validator.Validate(new AnalyzeVideoCommand(Guid.NewGuid()));

        invalid.IsValid.Should().BeFalse();
        valid.IsValid.Should().BeTrue();
    }

    [Fact]
    public void BatchAnalyzeVideosCommandValidator_ShouldValidateList()
    {
        var validator = new BatchAnalyzeVideosCommandValidator();

        var invalidEmpty = validator.Validate(new BatchAnalyzeVideosCommand(new List<Guid>()));
        var valid = validator.Validate(new BatchAnalyzeVideosCommand(new List<Guid> { Guid.NewGuid() }));

        invalidEmpty.IsValid.Should().BeFalse();
        valid.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task GetAiQuotas_And_ResetQuotas_ShouldWorkCorrectly()
    {
        var quotaManager = new FakeQuotaManager();

        var getHandler = new GetAiQuotasQueryHandler(quotaManager);
        var quotas = await getHandler.Handle(new GetAiQuotasQuery(), CancellationToken.None);

        quotas.Should().HaveCount(2);
        quotas[0].Provider.Should().Be("Cerebras");

        var resetHandler = new ResetAiQuotasCommandHandler(quotaManager);
        await resetHandler.Handle(new ResetAiQuotasCommand(), CancellationToken.None);

        quotaManager.ResetCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAiQuotasCommand_ShouldSyncGroqAndGemini()
    {
        var quotaManager = new FakeQuotaManager();
        var healthService = new FakeHealthService();

        var handler = new ValidateAiQuotasCommandHandler(healthService, quotaManager);
        var updatedQuotas = await handler.Handle(new ValidateAiQuotasCommand(), CancellationToken.None);

        healthService.SyncedProviders.Should().Contain(new[] { "Groq", "Gemini" });
        updatedQuotas.Should().HaveCount(2);
    }
}
