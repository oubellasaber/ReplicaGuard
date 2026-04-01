namespace ReplicaGuard.Core.Domain.Hoster;

/// <summary>
/// Links a CapabilityCode to the AuthenticationMethod infrastructure's worker demands for it.
/// Value object: has no identity of its own; exists only as part of Hoster.
/// Persisted via EF Core OwnsMany in a separate table.
/// </summary>
public sealed class FeatureRequirement
{
    public CapabilityCode Feature { get; private init; }
    public Credentials RequiredAuth { get; private init; }

    internal static FeatureRequirement Create(CapabilityCode feature, Credentials requiredAuth)
        => new() { Feature = feature, RequiredAuth = requiredAuth };
}

/// <summary>
/// Operations a hoster can be asked to perform.
/// Adding a new value is an intentional, deliberate code change.
/// </summary>
public enum CapabilityCode
{
    RemoteUpload,
    SpooledUpload,
    Download,
    CheckStatus
}

/// <summary>
/// The exact credential combination a hoster requires for a feature.
/// </summary>
[Flags]
public enum Credentials
{
    None = 0,
    ApiKey = 1 << 0,
    EmailPassword = 1 << 1,
    UsernamePassword = 1 << 2,
}
