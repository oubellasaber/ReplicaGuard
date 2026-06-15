using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Common;

namespace ReplicaGuard.Core.HosterAccounts;

public static class AuthIdentityErrors
{
    public static Error NotFound(Guid id) 
            => CommonErrors.NotFound(nameof(AuthIdentity), id);
}
