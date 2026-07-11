namespace ReplicaGuard.Application.Replication.UploadReplica.Spooling;

public enum SpoolStatus
{
    /// File does not exist locally and no lease is held.
    NotExist,
    /// A lease is held and the file is not yet fully written.
    Downloading,
    /// File is fully written. A stale lease may still be present.
    Completed,
}

public sealed record SpoolState(
    SpoolStatus Status,
    string FilePath,
    Guid? OwnerReplicaId,
    SpoolLease? Lease);

/// <summary>
/// Derives spool state from the file locator and an optional lease.
/// </summary>
public static class SpoolStateResolver
{
    public static SpoolState Resolve(
        Guid assetId,
        string fileName,
        ISpoolFileLocator locator,
        SpoolLease? lease)
    {
        var status = ResolveStatus(assetId, fileName, locator, lease);
        var filePath = locator.GetSpoolPath(assetId, fileName);

        return new SpoolState(
            Status: status,
            FilePath: filePath,
            OwnerReplicaId: lease?.OwnerReplicaId,
            Lease: lease);
    }

    public static SpoolStatus ResolveStatus(
        Guid assetId,
        string fileName,
        ISpoolFileLocator locator,
        SpoolLease? lease)
    {
        var fileExists = locator.IsSpooled(assetId, fileName);

        if (fileExists && lease is not null)
            return SpoolStatus.Downloading;

        if (fileExists)
            return SpoolStatus.Completed;

        return SpoolStatus.NotExist;
    }
}
