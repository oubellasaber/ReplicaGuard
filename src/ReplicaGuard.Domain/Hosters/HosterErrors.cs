using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Common;

namespace ReplicaGuard.Core.Hosters;

public static class HosterErrors
{
    public static Error NotFound(HosterCode hosterId)
        => CommonErrors.NotFound(nameof(HosterCode), hosterId.ToFriendlyString());

    public static Error NotFound(string friendlyName)
        => CommonErrors.NotFound(nameof(HosterCode), friendlyName);

    public static Error AccountDoesNotBelongToHoster(HosterCode hosterId, Guid accountId)
        => new Error(
                code: "Hoster.AccountMismatch",
                message: $"Account does not belong to hoster.",
                type: ErrorType.Validation)
            .WithMetadata("HosterId", hosterId.ToFriendlyString())
            .WithMetadata("AccountId", accountId);

    public static Error PrimaryIdentitiesNotSatisfied(HosterCode hosterId)
        => new Error(
                code: "Hoster.PrimaryIdentitiesNotSatisfied",
                message: $"Primary identity requirements not satisfied.",
                type: ErrorType.Validation)
            .WithMetadata("HosterId", hosterId.ToFriendlyString());
    public static Error CapabilityNotSupported(HosterCode hosterId, CapabilityCode capability)
        => new Error(
                code: "Hoster.CapabilityNotSupported",
                message: $"CapabilityCode not supported by this hoster.",
                type: ErrorType.Validation)
            .WithMetadata("HosterId", hosterId.ToFriendlyString())
            .WithMetadata("Capability", capability);

    public static Error CapabilityRequirementsNotSatisfied(HosterCode hosterId, CapabilityCode capability)
        => new Error(
                code: "Hoster.CapabilityRequirementsNotSatisfied",
                message: $"CapabilityCode requirements not satisfied.",
                type: ErrorType.Validation)
            .WithMetadata("HosterId", hosterId.ToFriendlyString())
            .WithMetadata("Capability", capability);
}
