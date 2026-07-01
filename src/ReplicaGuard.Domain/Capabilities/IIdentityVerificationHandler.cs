using ReplicaGuard.Domain.HosterAccounts;

namespace ReplicaGuard.Domain.Capabilities;

public record IdentityVerificationRequest(AuthIdentity Identity);

public interface IIdentityVerificationHandler : ICapabilityHandler<IdentityVerificationRequest> { }
