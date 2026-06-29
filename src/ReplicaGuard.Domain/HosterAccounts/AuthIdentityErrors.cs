using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Common;

namespace ReplicaGuard.Domain.HosterAccounts;

public static class AuthIdentityErrors
{
    public static Error NotFound(Guid id) 
            => CommonErrors.NotFound(nameof(AuthIdentity), id);
}
