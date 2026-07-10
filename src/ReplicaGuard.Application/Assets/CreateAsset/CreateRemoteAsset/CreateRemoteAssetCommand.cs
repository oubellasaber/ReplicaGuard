using ReplicaGuard.Application.Abstractions.Messaging;

namespace ReplicaGuard.Application.Assets.CreateAsset.CreateRemoteAsset;

public sealed record CreateRemoteAssetCommand(string Url, string FileName, List<HosterAccountDto> Hosters)
    : ICommand<CreateAssetResponse>;
