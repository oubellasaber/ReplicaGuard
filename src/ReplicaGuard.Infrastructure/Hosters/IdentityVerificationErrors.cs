using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Hosters;

namespace ReplicaGuard.Infrastructure.Hosters;

public static class IdentityVerificationErrors
{
    public static Error InvalidApiKey(HosterCode hosterId) =>
        new Error(
            code: "HosterAccount.Identity.InvalidApiKey",
            message: "The API key provided for hoster is invalid.")
        .WithMetadata("HosterId", hosterId.ToFriendlyString());
}
