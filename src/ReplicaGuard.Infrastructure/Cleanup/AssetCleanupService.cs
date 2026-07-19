using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReplicaGuard.Application.Assets.Services;
using ReplicaGuard.Application.Replication.UploadReplica.Fetching;
using ReplicaGuard.Application.Replication.UploadReplica.Spooling;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Replication;
using ReplicaGuard.Infrastructure.Persistence;
using ReplicaGuard.Infrastructure.Storage;

namespace ReplicaGuard.Infrastructure.Cleanup;

internal sealed class AssetCleanupService : IAssetCleanupService
{
    private readonly ApplicationDbContext _db;
    private readonly ISpoolFileLocator _spoolFileLocator;
    private readonly FileFetcherOptions _fetcherOptions;
    private readonly UserUploadsOptions _uploadsOptions;
    private readonly StorageOptions _storageOptions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AssetCleanupService> _logger;

    public AssetCleanupService(
        ApplicationDbContext db,
        ISpoolFileLocator spoolFileLocator,
        IOptions<FileFetcherOptions> fetcherOptions,
        IOptions<UserUploadsOptions> uploadsOptions,
        IOptions<StorageOptions> storageOptions,
        IUnitOfWork unitOfWork,
        ILogger<AssetCleanupService> logger)
    {
        _db = db;
        _spoolFileLocator = spoolFileLocator;
        _fetcherOptions = fetcherOptions.Value;
        _uploadsOptions = uploadsOptions.Value;
        _storageOptions = storageOptions.Value;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task CleanupAssetFilesAsync(Guid assetId, string fileName, CancellationToken ct = default)
    {
        var asset = await _db.Set<Asset>()
            .Where(a => a.Id == assetId)
            .FirstAsync(ct);

        if (asset?.Source is LocalFileSource local)
        {
            TryDelete(local.FilePath, "user upload");
        }

        if (asset?.Source is RemoteFileSource)
        {
            if (TryDelete(_spoolFileLocator.GetSpoolPath(assetId, fileName), "spool"))
                TryDelete(_spoolFileLocator.GetTempSpoolPath(assetId, fileName), "temp spool");
        }
    }

    public async Task<int> CleanupExpiredAssetsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var retentionDays = _storageOptions.RetentionDays;

        var expiredAssets = await _db.Set<Asset>()
            .Where(a => a.CleanupAfterUtc != null
                && a.CleanupAfterUtc <= now
                && a.CreatedAtUtc < now.AddDays(-retentionDays))
            .ToListAsync(ct);

        if (expiredAssets.Count == 0)
            return 0;

        int cleaned = 0;

        foreach (var asset in expiredAssets)
        {
            if (asset.Status is not (AssetStatus.Completed or AssetStatus.Failed))
                continue;

            await CleanupAssetFilesAsync(asset.Id, asset.FileName.Value, ct);
            asset.ClearCleanup();
            cleaned++;
        }

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Cleaned up {Count} expired assets", cleaned);

        return cleaned;
    }

    public Task<int> CleanupOrphanedTempFilesAsync(CancellationToken ct = default)
    {
        int cleaned = 0;

        cleaned += CleanupDirectoryTempFiles(_fetcherOptions.SpoolDirectory, "spl_*.tmp");
        cleaned += CleanupDirectoryTempFiles(_uploadsOptions.UploadDirectory, "upl_*.tmp");

        if (cleaned > 0)
            _logger.LogInformation("Cleaned up {Count} orphaned temp files", cleaned);

        return Task.FromResult(cleaned);
    }

    private bool TryDelete(string path, string label)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                _logger.LogInformation("Deleted {Label} file: {Path}", label, path);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete {Label} file: {Path}", label, path);
        }
        return false;
    }

    private int CleanupDirectoryTempFiles(string? directory, string pattern)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return 0;

        int cleaned = 0;
        var threshold = DateTime.UtcNow.AddHours(-2);

        foreach (var file in Directory.GetFiles(directory, pattern))
        {
            try
            {
                var info = new FileInfo(file);
                if (info.LastWriteTimeUtc < threshold)
                {
                    File.Delete(file);
                    _logger.LogInformation("Deleted orphaned temp file: {Path}", file);
                    cleaned++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete orphaned temp file: {Path}", file);
            }
        }

        return cleaned;
    }
}
