using ReplicaGuard.Domain.HosterAccounts;

namespace ReplicaGuard.Domain.Hosters;

// A requirement path specifies a set of identities that must all be present and be in a valid state to satisfy the path.
// Multiple paths in a CapabilityRequirement are OR‑ed together, but identities within a single RequirementPath are AND‑ed together.
public sealed class RequirementPath
{
    private readonly HashSet<IdentityType> _required;
    public IReadOnlyCollection<IdentityType> RequiredIdentities => _required;

    internal RequirementPath(IEnumerable<IdentityType> required)
    {
        _required = required.ToHashSet();
    }

    public bool IsSatisfiedBy(IEnumerable<AuthIdentity> identities, bool onlyVerified = true)
    {
        var provided = identities
            .Where(i => !onlyVerified || i.Status == IdentityVerificationStatus.Verified)
            .Select(i => i.Type)
            .ToHashSet();

        return _required.IsSubsetOf(provided);
    }
}
