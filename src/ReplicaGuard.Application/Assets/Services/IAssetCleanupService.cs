namespace ReplicaGuard.Application.Assets.Services;

public interface IAssetCleanupService
{
    /// Deletes spool + upload files for a specific asset.
    Task CleanupAssetFilesAsync(Guid assetId, string fileName, CancellationToken ct = default);

    /// Finds assets past their CleanupAfterUtc and deletes their files.
    Task<int> CleanupExpiredAssetsAsync(CancellationToken ct = default);

    /// Deletes orphaned .tmp files in spool and upload directories.
    Task<int> CleanupOrphanedTempFilesAsync(CancellationToken ct = default);
}
