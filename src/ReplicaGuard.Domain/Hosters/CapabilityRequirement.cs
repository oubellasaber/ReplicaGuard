using ReplicaGuard.Core.HosterAccounts;

namespace ReplicaGuard.Core.Hosters;

// A capability requirement specifies the identities required to use a specific capability of a hoster.
// Each RequirementPath is one OR‑branch. To satisfy the requirement, at least one of the paths must be satisfied.
public sealed class CapabilityRequirement
{
    public CapabilityCode Capability { get; }
    private readonly List<RequirementPath> _paths;

    public IReadOnlyList<RequirementPath> Paths => _paths;

    internal CapabilityRequirement(
        CapabilityCode capability,
        IEnumerable<RequirementPath> paths)
    {
        Capability = capability;
        _paths = paths.ToList();
    }

    public bool IsSatisfiedBy(IEnumerable<AuthIdentity> identities)
        => _paths.Any(path => path.IsSatisfiedBy(identities));

    public override string ToString()
    {
        return $"{string.Join(" OR ", _paths.Select(p => string.Join(" AND ", p.RequiredIdentities)))}";
    }
}
