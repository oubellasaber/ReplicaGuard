namespace ReplicaGuard.Api.Controllers.Assets;

public sealed record CreateAssetRequest(
    string Source,
    string FileName,
    List<HosterDto> Hosters);

public sealed record HosterDto(string HosterId, Guid AccountId);
