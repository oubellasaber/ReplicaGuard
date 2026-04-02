using ReplicaGuard.Application.Abstractions.Messaging;

namespace ReplicaGuard.Application.Assets.CreateAsset;

public sealed record CreateAssetCommand(
    string Source,
    string FileName,
    List<Guid> HosterIds) : ICommand<CreateAssetResponse>;
