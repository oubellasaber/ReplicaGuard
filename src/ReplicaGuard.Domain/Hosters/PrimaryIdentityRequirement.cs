using ReplicaGuard.Domain.HosterAccounts;

namespace ReplicaGuard.Domain.Hosters;

public sealed class PrimaryIdentityRequirement
{
    private readonly List<RequirementPath> _paths;
    public IReadOnlyList<RequirementPath> Paths => _paths;

    public PrimaryIdentityRequirement(IEnumerable<RequirementPath> paths)
    {
        _paths = paths.ToList();
    }

    public bool IsSatisfiedBy(IEnumerable<AuthIdentity> identities, bool onlyVerified = true)
        => _paths.Any(path => path.IsSatisfiedBy(identities, onlyVerified));
}
