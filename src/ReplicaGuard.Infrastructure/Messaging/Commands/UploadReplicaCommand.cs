namespace ReplicaGuard.Infrastructure.Messaging.Commands;

public sealed record UploadReplicaCommand(Guid UserId, Guid AssetId, Guid ReplicaId);
