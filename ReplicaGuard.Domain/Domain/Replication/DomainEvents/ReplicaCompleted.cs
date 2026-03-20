using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Core.Domain.Replication.DomainEvents;

public sealed record ReplicaCompleted(Guid ReplicaId, Guid AssetId, Guid HosterId, Uri Link) : IDomainEvent;
