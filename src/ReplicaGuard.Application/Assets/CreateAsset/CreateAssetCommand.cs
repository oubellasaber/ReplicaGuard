using ReplicaGuard.Application.Abstractions.Messaging;

namespace ReplicaGuard.Application.Assets.CreateAsset;

internal sealed record CreateAssetCommand(
    string Source,
    string FileName,
    IEnumerable<Guid> HosterAccountIds) : ICommand<CreateAssetResponse>;
