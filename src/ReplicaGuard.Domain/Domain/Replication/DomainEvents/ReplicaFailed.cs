using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Core.Domain.Replication.DomainEvents;

public sealed record ReplicaFailed(Guid ReplicaId, Guid AssetId, Guid HosterId, string Reason) : IDomainEvent;
