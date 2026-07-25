using ReplicaGuard.Application.Abstractions.Messaging;

namespace ReplicaGuard.Application.Replicas.GenerateDownloadUrl;

public sealed record GenerateDownloadUrlCommand(Guid AssetId, Guid ReplicaId) : ICommand<GenerateDownloadUrlResponse>;