using ReplicaGuard.Domain.Hosters;

namespace ReplicaGuard.Domain.Capabilities;

public interface ICapabilityFactory
{
    T Get<T>(HosterCode hoster) where T : class;
}
