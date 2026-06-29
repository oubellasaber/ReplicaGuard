namespace ReplicaGuard.Domain.Replication;

/// <summary>
/// Repository for Replica entity.
/// </summary>
public interface IReplicaRepository
{
    /// <summary>
    /// Get a replica by its ID.
    /// </summary>
    Task<Replica?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a batch of pending replicas for processing.
    /// </summary>
    Task<List<Replica>> GetPendingReplicasAsync(int batchSize, CancellationToken cancellationToken = default);

    Task<MarkWaitingResult> TryMarkWaitingIfDownloaderStillActive(
        Guid assetId,
        Guid replicaId,
        CancellationToken ct);
}

public enum MarkWaitingResult
{
    MarkedWaiting,
    AlreadyCompleted,
    NoActiveDownloader
}
