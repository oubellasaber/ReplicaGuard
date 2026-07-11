namespace ReplicaGuard.Application.Abstractions.Storage;

public enum DiskStatusLevel
{
    Healthy,
    Low,
    Critical
}

public sealed record DiskStatus(
    long FreeBytes,
    long TotalBytes,
    DiskStatusLevel Level);

public interface IStorageMonitor
{
    /// Checks disk free space for the volume that contains the given directory.
    DiskStatus GetStatus(string directoryPath);
}
