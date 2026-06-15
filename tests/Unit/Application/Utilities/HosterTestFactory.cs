using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Hosters;

namespace ReplicaGuard.Application.Tests.Utilities;

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
