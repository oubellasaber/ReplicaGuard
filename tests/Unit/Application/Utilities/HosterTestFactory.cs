using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Hoster;

namespace ReplicaGuard.Application.Tests.Testing;

internal static class HosterTestFactory
{
    internal static Hoster CreateWithId(Guid id, string code, Credentials primaryCredentials)
    {
        Hoster hoster = Hoster.Create(code, code, primaryCredentials).Value;
        typeof(Entity<Guid>).GetProperty(nameof(Entity<Guid>.Id))!
            .SetValue(hoster, id);

        return hoster;
    }
}
