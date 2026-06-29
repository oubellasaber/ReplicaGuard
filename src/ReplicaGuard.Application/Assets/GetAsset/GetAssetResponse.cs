namespace ReplicaGuard.Application.Assets.GetAsset;

public sealed record GetAssetResponse(
    Guid Id,
    string FileName,
    string Status,
    long? SizeBytes,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    List<ReplicaResponse> Replicas);

public sealed record ReplicaResponse(
    Guid Id,
    Guid HosterId,
    Guid? AccountId,
    string Status,
    string? Link,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
