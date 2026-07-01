using ReplicaGuard.Core.HosterAccounts;

namespace ReplicaGuard.Core.Capabilities;

public record IdentityVerificationRequest(AuthIdentity Identity);

public interface IIdentityVerificationHandler : ICapabilityHandler<IdentityVerificationRequest> { }
