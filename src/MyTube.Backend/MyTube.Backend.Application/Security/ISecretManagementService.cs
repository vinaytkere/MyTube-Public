using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyTube.Backend.Application.Security;

/// <summary>
/// Centralized service for secure retrieval of credentials, API keys, and sensitive configuration.
/// Follows the Single Responsibility Principle: all secret sourcing (Infisical, Environment, Config)
/// is encapsulated exclusively in this service.
/// </summary>
public interface ISecretManagementService
{
    /// <summary>
    /// Asynchronously retrieves a secret by key.
    /// Hierarchy: In-memory cache -> Infisical Vault -> Environment variables -> Configuration -> Default value.
    /// </summary>
    Task<string?> GetSecretAsync(string secretKey, string? defaultValue = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronously retrieves a secret by key.
    /// </summary>
    string? GetSecret(string secretKey, string? defaultValue = null);

    /// <summary>
    /// Asynchronously retrieves multiple secrets in a batch.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetSecretsAsync(IEnumerable<string> secretKeys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates the local cache for a specific secret, or clears all cached secrets if null.
    /// </summary>
    void InvalidateCache(string? secretKey = null);
}
