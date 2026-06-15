using MediatR;
using ReplicaGuard.Application.Abstractions.Messaging;

namespace ReplicaGuard.Application.Replication.UploadReplica;

public sealed record UploadReplicaCommand(Guid UserId, Guid AssetId, Guid ReplicaId) : ICommand;
