using ReplicaGuard.Application.Abstractions.Messaging;

namespace ReplicaGuard.Application.Assets.CreateAsset;

internal sealed record CreateAssetCommand(
    string Source,
    string FileName,
    IEnumerable<Guid> HosterAccountIds,
    Guid? AssetId = null,
    string? BaseDirectory = null) : ICommand<CreateAssetResponse>;
