using ReplicaGuard.Application.Abstractions.Messaging;

namespace ReplicaGuard.Application.Assets.CreateAsset.CreateLocalAsset;

public sealed record CreateLocalAssetCommand(Guid AssetId, string BaseDirectory, string FilePath, string FileName, IEnumerable<Guid> HosterAccountIds)
    : ICommand<CreateAssetResponse>;
