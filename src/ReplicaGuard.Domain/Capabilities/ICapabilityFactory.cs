using ReplicaGuard.Core.Hosters;

namespace ReplicaGuard.Core.Capabilities;

public interface ICapabilityFactory
{
    T Get<T>(HosterCode hoster) where T : class;
}
