namespace ReplicaGuard.Api.Controllers.Assets;

public sealed record CreateAssetRequest(
    string Source,
    string FileName,
    List<Guid> HosterIds);
