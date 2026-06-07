using MediatR;
using ReplicaGuard.Application.Abstractions.Messaging;

namespace ReplicaGuard.Application.Replication.UploadReplica;

public sealed record UploadReplicaCommand(
    Guid ReplicaId,
    Guid AssetId,
    Guid HosterId,
    bool IsLastRetry) : ICommand<Unit>;
