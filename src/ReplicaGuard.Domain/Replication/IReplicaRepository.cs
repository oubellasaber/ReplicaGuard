using System.Threading.Tasks;

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

    Task<MarkWaitingResult> TryMarkWaitingIfDownloaderStillActive(
        Guid assetId,
        Guid replicaId,
        CancellationToken ct);

    Task<IReadOnlyList<Replica>> GetReplicasNearExpiryAsync(
        DateTime utcNow,
        TimeSpan window,
        int batchSize,
        CancellationToken ct);
}

public enum MarkWaitingResult
{
    MarkedWaiting,
    AlreadyCompleted,
    NoActiveDownloader
}
