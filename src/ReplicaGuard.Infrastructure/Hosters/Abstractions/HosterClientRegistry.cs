using ReplicaGuard.Core.Capabilities;
using ReplicaGuard.Core.Domain.Hoster;

namespace ReplicaGuard.Infrastructure.Hosters.Abstractions;
public class HosterClientRegistry : IHosterClientRegistry
{
    private readonly Dictionary<(string hoster, Type capability), object> _map = new();

    public HosterClientRegistry(IEnumerable<IHosterApiClient> services)
    {
        foreach (var service in services)
        {
            var type = service.GetType();
            string hosterCode = service.Code.ToUpper();

            // get ALL interfaces the class implements (automatic)
            var interfaces = type.GetInterfaces()
                                 .Where(i => i != typeof(IDisposable) &&
                                             !i.Namespace!.StartsWith("System"));

            foreach (var iface in interfaces)
            {
                _map[(hosterCode, iface)] = service;
            }
        }
    }

    public TCapability? TryGetHosterCapability<TCapability>(string hosterCode)
        where TCapability : class
    {
        return _map.TryGetValue((hosterCode, typeof(TCapability)), out var instance)
            ? (TCapability)instance
            : null;
    }

    public TCapability GetHosterCapability<TCapability>(string hosterCode)
        where TCapability : class
    {
        if (!_map.TryGetValue((hosterCode, typeof(TCapability)), out var instance))
            throw new InvalidOperationException(
                $"Hoster '{hosterCode}' does not support {typeof(TCapability).Name}");

        return (TCapability)instance;
    }
}
