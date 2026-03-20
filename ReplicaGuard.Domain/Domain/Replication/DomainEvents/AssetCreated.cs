using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Core.Domain.Replication.DomainEvents;

public sealed record AssetCreated(Guid AssetId, Guid UserId, string FileName) : IDomainEvent;
