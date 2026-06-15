using ReplicaGuard.Core.Hosters;

namespace ReplicaGuard.Core.HosterAccounts;

public class IdentityGroup
{
    public HosterCode HosterId { get; }
    public IReadOnlyList<IdentityType> GroupedIdentites { get; }

    public IdentityGroup(
        HosterCode hosterId,
        IEnumerable<IdentityType> groupedIdentites)
    {
        HosterId = hosterId;
        GroupedIdentites = groupedIdentites.ToList();
    }
}
