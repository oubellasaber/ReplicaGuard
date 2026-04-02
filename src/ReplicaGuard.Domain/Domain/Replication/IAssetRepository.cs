namespace ReplicaGuard.Core.Domain.Replication;

/// <summary>
/// Repository for Asset aggregate.
/// </summary>
public interface IAssetRepository
{
    /// <summary>
    /// Add a new asset to the repository.
    /// </summary>
    void Add(Asset asset);

    /// <summary>
    /// Get an asset by its ID.
    /// </summary>
    Task<Asset?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get an asset by its ID with all replicas eagerly loaded.
    /// </summary>
    Task<Asset?> GetByIdWithReplicasAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all assets belonging to a specific user.
    /// </summary>
    Task<List<Asset>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
