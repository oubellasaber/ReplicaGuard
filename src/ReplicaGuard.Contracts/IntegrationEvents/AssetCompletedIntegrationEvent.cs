namespace ReplicaGuard.Contracts.IntegrationEvents;

public sealed record AssetCompletedIntegrationEvent(
    Guid AssetId,
    Guid ReplicaId,
    int ReplicaStatus);
