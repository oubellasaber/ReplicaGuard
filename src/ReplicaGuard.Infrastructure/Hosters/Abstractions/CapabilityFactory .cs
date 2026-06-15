using Microsoft.Extensions.DependencyInjection;
using ReplicaGuard.Core.Capabilities;
using ReplicaGuard.Core.Hosters;

namespace ReplicaGuard.Infrastructure.Hosters.Abstractions;

public sealed class CapabilityFactory : ICapabilityFactory
{
    private readonly IServiceProvider _sp;
    private readonly IReadOnlyDictionary<(HosterCode, CapabilityCode, Type), Type> _map;

    public CapabilityFactory(IServiceProvider sp, IEnumerable<Type> capabilityImplementationTypes)
    {
        _sp = sp ?? throw new ArgumentNullException(nameof(sp));
        if (capabilityImplementationTypes == null) throw new ArgumentNullException(nameof(capabilityImplementationTypes));

        var map = new Dictionary<(HosterCode, CapabilityCode, Type), Type>();

        foreach (var implType in capabilityImplementationTypes)
        {
            // instantiate once to read interface-backed properties (handles explicit interface impl)
            var implInstance = sp.GetRequiredService(implType);

            // find the generic capability interface implemented by this type (either ICapabilityHandler<> or ICapabilityHandler<,>)
            var genericCapabilityIface = implType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType &&
                                     (i.GetGenericTypeDefinition() == typeof(ICapabilityHandler<>) ||
                                      i.GetGenericTypeDefinition() == typeof(ICapabilityHandler<,>)));

            if (genericCapabilityIface == null)
                throw new InvalidOperationException($"{implType.FullName} does not implement ICapabilityHandler<> or ICapabilityHandler<,>.");

            // read Hoster and Capability from the interface PropertyInfo so explicit impls are supported
            var hosterProp = genericCapabilityIface.GetProperty(nameof(ICapabilityHandler<object>.HosterCode))
                            ?? genericCapabilityIface.GetProperty(nameof(ICapabilityHandler<object, object>.HosterCode));
            var capabilityProp = genericCapabilityIface.GetProperty(nameof(ICapabilityHandler<object>.CapabilityCode))
                               ?? genericCapabilityIface.GetProperty(nameof(ICapabilityHandler<object, object>.CapabilityCode));

            if (hosterProp == null || capabilityProp == null)
                throw new InvalidOperationException($"Interface {genericCapabilityIface} must expose Host er and CapabilityCode properties.");

            var hoster = (HosterCode)hosterProp.GetValue(implInstance)!;
            var capability = (CapabilityCode)capabilityProp.GetValue(implInstance)!;

            // prefer a specialized interface (e.g. IUploadCapability) if present; otherwise use the generic capability interface
            var specializedIface = implType.GetInterfaces()
                .FirstOrDefault(i => i != genericCapabilityIface &&
                                     i.IsInterface &&
                                     i.GetInterfaces().Any(g => g.IsGenericType &&
                                                               (g.GetGenericTypeDefinition() == typeof(ICapabilityHandler<>) ||
                                                                g.GetGenericTypeDefinition() == typeof(ICapabilityHandler<,>))))
                ?? genericCapabilityIface;

            var key = (hoster, capability, specializedIface);

            if (map.ContainsKey(key))
                throw new InvalidOperationException($"Duplicate capability registration for hoster={hoster} capability={capability} interface={specializedIface.Name}");

            map[key] = implType;
        }

        _map = map;
    }

    public T Get<T>(HosterCode hoster) where T : class
    {
        var iface = typeof(T);

        // find any key that uses this interface to discover the capability code
        var anyKey = _map.Keys.FirstOrDefault(k => k.Item3 == iface);
        if (anyKey.Equals(default((HosterCode, CapabilityCode, Type))))
            throw new InvalidOperationException($"No implementations registered for interface {iface.Name}");

        var capabilityCode = anyKey.Item2;

        if (!_map.TryGetValue((hoster, capabilityCode, iface), out var implType))
            throw new InvalidOperationException($"HosterCode {hoster} does not support capability {iface.Name}");

        return (T)_sp.GetRequiredService(implType);
    }
}
