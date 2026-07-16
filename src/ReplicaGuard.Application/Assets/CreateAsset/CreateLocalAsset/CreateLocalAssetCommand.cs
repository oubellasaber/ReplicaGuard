using ReplicaGuard.Application.Abstractions.Messaging;

namespace ReplicaGuard.Application.Assets.CreateAsset.CreateLocalAsset;

public sealed record CreateLocalAssetCommand(string FilePath, string FileName, IEnumerable<Guid> HosterAccountIds)
    : ICommand<CreateAssetResponse>;
