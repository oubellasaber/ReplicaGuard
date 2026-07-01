using ReplicaGuard.Domain.Abstractions;

namespace ReplicaGuard.Domain.HosterAccounts;

public sealed record IdentityCreatedDomainEvent(Guid IdentityId) : IDomainEvent;
