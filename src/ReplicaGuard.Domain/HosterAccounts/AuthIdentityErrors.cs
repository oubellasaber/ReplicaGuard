using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Common;

namespace ReplicaGuard.Domain.HosterAccounts;

public static class AuthIdentityErrors
{
    public static Error NotFound(Guid id) 
            => CommonErrors.NotFound(nameof(AuthIdentity), id);

    public static Error IdentityNotVerified(Guid accountId, Guid identityId) =>
        new Error($"Identity.NotVerified", "The hoster account identity is not verified.")
            .WithType(ErrorType.Forbidden)
            .WithMetadata(nameof(accountId), accountId)
            .WithMetadata(nameof(identityId), identityId)
            .AsPermanent();

    public static Error IdentityMissing(Guid accountId, IdentityType identityType) =>
        new Error($"Identity.Missing", "The hoster account is missing a required identity.")
            .WithType(ErrorType.Forbidden)
            .WithMetadata(nameof(accountId), accountId)
            .WithMetadata(nameof(identityType), identityType.ToString())
            .AsPermanent();
}
