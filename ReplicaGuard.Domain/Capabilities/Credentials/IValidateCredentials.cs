using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Core.Capabilities.Credentials;

/// <summary>
/// Validates the authentication credentials for a hoster.
/// All hosters must implement this capability.
/// </summary>
public interface IValidateCredentials
{
    /// <summary>
    /// Validates credentials and returns them validated.
    /// </summary>
    Task<Result<CredentialSet>> ValidateAsync(
        CredentialSet credentials,
        CancellationToken ct = default);
}

/// <summary>
/// Immutable credential set for validation.
/// </summary>
public sealed record CredentialSet(
    string? ApiKey = null,
    string? Email = null,
    string? Username = null,
    string? Password = null);
