namespace ReplicaGuard.Contracts.IntegrationEvents;

public sealed record ReplicaExpiringSoonIntegrationEvent(
    Guid ReplicaId,
    Guid AssetId,
    Guid HosterId);

