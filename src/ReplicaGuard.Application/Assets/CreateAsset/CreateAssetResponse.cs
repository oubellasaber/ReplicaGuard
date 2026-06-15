namespace ReplicaGuard.Application.Assets.CreateAsset;

public sealed record CreateAssetResponse(
    Guid AssetId,
    string FileName,
    string Status,
    int ReplicaCount,
    DateTime CreatedAtUtc);
