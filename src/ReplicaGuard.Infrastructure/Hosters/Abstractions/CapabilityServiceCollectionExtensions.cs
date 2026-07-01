using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ReplicaGuard.Domain.Capabilities;

namespace ReplicaGuard.Infrastructure.Hosters.Abstractions;

public static class CapabilityServiceCollectionExtensions
{
    public static IServiceCollection AddHosterCapabilitiesFromAssemblies(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        // 1. Find all capability handler implementations (direct or inherited)
        var implTypes = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(ImplementsCapabilityHandler)
            .ToList();

        foreach (var impl in implTypes)
        {
            // Register concrete implementation
            services.AddTransient(impl);

            // Register specialized interfaces (e.g. IIdentityVerificationHandler)
            var specializedIfaces = impl
                .GetInterfaces()
                .Where(i => i.IsInterface && IsSpecializedCapabilityInterface(i))
                .ToList();

            foreach (var iface in specializedIfaces)
                services.AddTransient(iface, sp => sp.GetRequiredService(impl));
        }

        // Register factory with only capability implementation types
        services.AddSingleton<ICapabilityFactory>(sp => new CapabilityFactory(sp, implTypes));

        return services;
    }

    // ------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------

    private static bool ImplementsCapabilityHandler(Type t) =>
        GetAllInterfaces(t).Any(IsGenericCapabilityInterface);

    private static bool IsSpecializedCapabilityInterface(Type iface) =>
        GetAllInterfaces(iface).Any(IsGenericCapabilityInterface);

    private static bool IsGenericCapabilityInterface(Type iface) =>
        iface.IsGenericType &&
        (
            iface.GetGenericTypeDefinition() == typeof(ICapabilityHandler<>) ||
            iface.GetGenericTypeDefinition() == typeof(ICapabilityHandler<,>)
        );

    private static IEnumerable<Type> GetAllInterfaces(Type t) =>
        t.GetInterfaces()
         .SelectMany(i => new[] { i }.Concat(GetAllInterfaces(i)))
         .Distinct();
}
