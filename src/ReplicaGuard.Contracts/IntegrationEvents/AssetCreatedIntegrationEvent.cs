namespace ReplicaGuard.Contracts.IntegrationEvents;

public sealed record AssetCreatedIntegrationEvent(
    Guid UserId,
    Guid AssetId,
    IReadOnlyCollection<Guid> ReplicasIds
);
