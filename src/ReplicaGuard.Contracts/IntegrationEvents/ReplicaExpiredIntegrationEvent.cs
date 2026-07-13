namespace ReplicaGuard.Contracts.IntegrationEvents;

public sealed record ReplicaExpiredIntegrationEvent(
    Guid ReplicaId,
    Guid AssetId,
    Guid HosterId);
