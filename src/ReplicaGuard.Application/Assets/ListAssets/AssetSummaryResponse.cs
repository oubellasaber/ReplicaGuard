namespace ReplicaGuard.Application.Assets.ListAssets;

public sealed record AssetSummaryResponse(
    Guid Id,
    string FileName,
    string Status,
    long? SizeBytes,
    int TotalReplicas,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
