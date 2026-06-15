using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Core.Hosters;

namespace ReplicaGuard.Application.Assets.CreateAsset;

public sealed record CreateAssetCommand(
    string Source,
    string FileName,
    List<HosterAccountDto> Hosters) : ICommand<CreateAssetResponse>;

public sealed record HosterAccountDto(
    HosterCode HosterId,
    Guid HosterAccountId);
