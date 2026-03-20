namespace ReplicaGuard.Application.Assets.ListAssets;

public sealed record AssetSummaryResponse(
    Guid Id,
    string FileName,
    string State,
    long? SizeBytes,
    int TotalReplicas,
    int CompletedReplicas,
    int FailedReplicas,
    DateTime CreatedAtUtc);
