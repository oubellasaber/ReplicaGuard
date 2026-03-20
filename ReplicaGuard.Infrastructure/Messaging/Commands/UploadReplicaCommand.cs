namespace ReplicaGuard.Infrastructure.Messaging.Commands;

public sealed record UploadReplicaCommand(
    Guid ReplicaId,
    Guid AssetId,
    Guid HosterId);
