using ReplicaGuard.Application.Abstractions.Messaging;

namespace ReplicaGuard.Application.Assets.CreateAsset;

internal sealed record CreateAssetCommand(
    string Source,
    string FileName,
    List<HosterAccountDto> Hosters) : ICommand<CreateAssetResponse>;

public sealed record HosterAccountDto(
    Guid HosterId,
    Guid HosterAccountId);
