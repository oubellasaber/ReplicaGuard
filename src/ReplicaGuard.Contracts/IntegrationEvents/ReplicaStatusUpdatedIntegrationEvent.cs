namespace ReplicaGuard.Contracts.IntegrationEvents;
public sealed record ReplicaStatusUpdatedIntegrationEvent(Guid ReplicaId, int Status, DateTime OccurredAt, long? TransferredBytes);
