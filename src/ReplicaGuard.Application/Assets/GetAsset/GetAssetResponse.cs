namespace ReplicaGuard.Application.Assets.GetAsset;

public sealed record GetAssetResponse(
    Guid Id,
    string FileName,
    string State,
    long? SizeBytes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    List<ReplicaResponse> Replicas);

public sealed record ReplicaResponse(
    Guid Id,
    Guid HosterId,
    string State,
    string? Link,
    string? LastError,
    int RetryCount,
    DateTime CreatedAtUtc);
