namespace ReplicaGuard.Application.Assets.CreateAsset;

public sealed record CreateAssetResponse(
    Guid AssetId,
    string FileName,
    string State,
    int ReplicaCount,
    DateTime CreatedAtUtc);
