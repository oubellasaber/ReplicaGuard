using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Core.HosterAccounts;

public sealed record IdentityCreatedDomainEvent(Guid IdentityId) : IDomainEvent;
