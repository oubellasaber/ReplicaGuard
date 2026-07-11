using Microsoft.Extensions.Options;
using ReplicaGuard.Application.Abstractions.Storage;

namespace ReplicaGuard.Infrastructure.Storage;

internal sealed class DiskSpaceMonitor : IStorageMonitor
{
    private readonly StorageOptions _options;

    public DiskSpaceMonitor(IOptions<StorageOptions> options)
    {
        _options = options.Value;
    }

    public DiskStatus GetStatus(string directoryPath)
    {
        var root = Path.GetPathRoot(directoryPath);

        if (string.IsNullOrEmpty(root))
            return new DiskStatus(0, 0, DiskStatusLevel.Critical);

        var driveInfo = new DriveInfo(root);

        long freeBytes = driveInfo.AvailableFreeSpace;
        long totalBytes = driveInfo.TotalSize;

        DiskStatusLevel level = DiskStatusLevel.Healthy;

        if (freeBytes <= 0 || freeBytes < _options.MinFreeBytes)
        {
            level = DiskStatusLevel.Critical;
        }
        else if (freeBytes < _options.MinFreeBytes * 2)
        {
            level = DiskStatusLevel.Low;
        }

        return new DiskStatus(freeBytes, totalBytes, level);
    }
}
