using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MyTube.Backend.Infrastructure.Security;
using Xunit;

namespace MyTube.Backend.Tests;

public class SecretManagementServiceTests : IDisposable
{
    private readonly List<string> _envVarsToClean = new();

    private void SetEnv(string key, string? value)
    {
        Environment.SetEnvironmentVariable(key, value);
        if (!_envVarsToClean.Contains(key))
        {
            _envVarsToClean.Add(key);
        }
    }

    public void Dispose()
    {
        foreach (var key in _envVarsToClean)
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    private class DummyHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new HttpClient();
    }

    [Fact]
    public async Task GetSecretAsync_ShouldReturnDefaultValue_WhenKeyNotFound()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var service = new SecretManagementService(
            new DummyHttpClientFactory(),
            configuration,
            NullLogger<SecretManagementService>.Instance);

        // Act
        var secret = await service.GetSecretAsync("NonExistent:Key", "fallback-default");

        // Assert
        secret.Should().Be("fallback-default");
    }

    [Fact]
    public async Task GetSecretAsync_ShouldReadFromConfiguration_WhenPresent()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Brevo:ApiKey", "cfg-secret-123" }
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var service = new SecretManagementService(
            new DummyHttpClientFactory(),
            configuration,
            NullLogger<SecretManagementService>.Instance);

        // Act
        var secret = await service.GetSecretAsync("Brevo:ApiKey");

        // Assert
        secret.Should().Be("cfg-secret-123");
    }

    [Fact]
    public async Task GetSecretAsync_ShouldPreferEnvironmentVariable_OverConfiguration()
    {
        // Arrange
        SetEnv("BREVO_API_KEY", "env-secret-456");

        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Brevo:ApiKey", "cfg-secret-123" }
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var service = new SecretManagementService(
            new DummyHttpClientFactory(),
            configuration,
            NullLogger<SecretManagementService>.Instance);

        // Act
        var secret = await service.GetSecretAsync("Brevo:ApiKey");

        // Assert
        secret.Should().Be("env-secret-456");
    }

    [Fact]
    public async Task InvalidateCache_ShouldPurgeCachedSecret()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Test:Secret", "initial-value" }
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var service = new SecretManagementService(
            new DummyHttpClientFactory(),
            configuration,
            NullLogger<SecretManagementService>.Instance);

        var firstRead = await service.GetSecretAsync("Test:Secret");
        firstRead.Should().Be("initial-value");

        // Act
        service.InvalidateCache("Test:Secret");

        // Assert - cache is cleared, will read again
        var secondRead = await service.GetSecretAsync("Test:Secret");
        secondRead.Should().Be("initial-value");
    }

    [Fact]
    public async Task GetSecretsAsync_ShouldRetrieveBatchOfSecrets()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "ServiceA:Key", "key-a" },
            { "ServiceB:Key", "key-b" }
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var service = new SecretManagementService(
            new DummyHttpClientFactory(),
            configuration,
            NullLogger<SecretManagementService>.Instance);

        // Act
        var secrets = await service.GetSecretsAsync(new[] { "ServiceA:Key", "ServiceB:Key" });

        // Assert
        secrets.Should().HaveCount(2);
        secrets["ServiceA:Key"].Should().Be("key-a");
        secrets["ServiceB:Key"].Should().Be("key-b");
    }
}
