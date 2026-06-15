using ReplicaGuard.Core.HosterAccounts;

namespace ReplicaGuard.Core.Capabilities;

public record IdentityVerificationRequest(AuthIdentity identity);

public interface IIdentityVerificationHandler : ICapabilityHandler<IdentityVerificationRequest> { }
