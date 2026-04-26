using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Core.Domain.Replication.DomainEvents;

public sealed record AllReplicasCompleted(Guid AssetId) : IDomainEvent;
