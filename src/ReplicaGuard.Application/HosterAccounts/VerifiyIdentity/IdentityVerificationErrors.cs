using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Hosters;

namespace ReplicaGuard.Application.HosterAccounts.VerifiyIdentity;

internal static class IdentityVerificationErrors
{
    public static Error NotSupported(HosterCode hosterId)
        => new Error(
            code: "IdentityVerification.NotSupported",
            message: $"Hoster does not support identity verification."
        )
        .WithMetadata("HosterId", hosterId.ToFriendlyString())
        .WithDetail($"The specified hoster does not have an identity verification handler implemented. Only hosters that require identity verification for their accounts will have this implemented.")
        .WithType(ErrorType.Validation)
        .AsPermanent();
}
