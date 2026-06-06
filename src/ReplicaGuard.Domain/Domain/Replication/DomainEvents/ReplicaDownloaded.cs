using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Core.Domain.Replication.DomainEvents;

public sealed record ReplicaDownloaded(Guid ReplicaId, DateTime UtcNow) : IDomainEvent;
