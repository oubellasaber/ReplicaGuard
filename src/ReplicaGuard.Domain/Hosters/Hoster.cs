using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Core.Hosters;

/// <summary>
/// Represents a hoster provider (e.g., "pixeldrain", "send", "krakenfiles")
/// </summary>
public sealed class Hoster : Entity<HosterCode>
{
    /// <summary>
    /// Human-readable name of the hoster.
    /// </summary>
    public string DisplayName { get; }
    public DateTime CreatedAtUtc { get; }
    public DateTime UpdatedAtUtc { get; private set; }

    ///// <summary>
    ///// OR-of-ANDs rule describing which identities are required
    ///// for an account to be considered valid for this hoster.
    ///// </summary>
    //public PrimaryIdentityRequirement PrimaryIdentities { get; }

    //private readonly List<CapabilityRequirement> _capabilityRequirements;

    ///// <summary>
    ///// Capability-specific OR-of-ANDs identity requirements.
    ///// </summary>
    //public IReadOnlyList<CapabilityRequirement> CapabilityRequirements => _capabilityRequirements;

    //private readonly List<IdentityGroup> _identityGroups;
    //public IReadOnlyList<IdentityGroup> IdentityGroups => _identityGroups;

    public Hoster(HosterCode id, string displayName)
        : base(id)
    {
        DisplayName = displayName;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }
}
