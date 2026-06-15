using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Common;
using ReplicaGuard.Core.Hosters;

namespace ReplicaGuard.Core.HosterAccounts;

public sealed class HosterAccountErrors
{
    public static Error NotFound(Guid id) 
        => CommonErrors.NotFound(nameof(HosterAccount), id);

    public static Error IdentityIsUnusable(Guid accountId, IdentityType type)
        => new Error(
                code: "HosterAccount.IdentityUnusable",
                message: $"The provided identity is in an unusable state.",
                type: ErrorType.Validation)
            .WithDetail("The identity must be verified and not expired, rejected, or pending.")
            .WithMetadata("AccountId", accountId)
            .WithMetadata("IdentityType", type);

    public static Error IdentitiesAreUnusable(Guid accountId, IEnumerable<IdentityType> types)
        => new Error(
                code: "HosterAccount.IdentitiesUnusable",
                message: $"The provided identities are in an unusable state.",
                type: ErrorType.Validation)
            .WithDetail("The identities must be verified and not expired, rejected, or pending.")
            .WithMetadata("AccountId", accountId)
            .WithMetadata("IdentityTypes", types);

    public static Error PrimaryIdentitiesNotSatisfied(PrimaryIdentityRequirement requirement, HosterCode hoster)
        => new Error(
            code: "Hoster.PrimaryIdentitiesNotSatisfied",
            message: $"Provided identities do not satisfy the hoster's primary identity requirement.",
            type: ErrorType.Validation)
        .WithMetadata("HosterId", hoster.ToFriendlyString())
        .WithMetadata("PrimaryIdentities", requirement);

    public static Error RequiredIdentitesNotSatisfied(CapabilityRequirement requirement, HosterCode hoster, CapabilityCode capability)
        => new Error(
            code: "Hoster.CapabilityRequirementsNotSatisfied",
            message: $"Provided identities do not satisfy the hoster's capability requirements.",
            type: ErrorType.Validation)
        .WithMetadata("HosterId", hoster.ToFriendlyString())
        .WithMetadata("Capability", capability)
        .WithMetadata("RequiredIdentities", requirement);

    public static Error NoAccountSetUp(Guid userId, IEnumerable<HosterCode> hosterIds) => new Error(
        code: "HosterAccount.NoAccountSetUp",
        message: "The user does not have any accounts set up for the specified hosters.",
        type: ErrorType.Validation)
        .WithMetadata("UserId", userId)
        .WithMetadata("HosterIds", hosterIds.Select(h => h.ToFriendlyString()));
}
