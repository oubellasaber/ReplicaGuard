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
    Guid? OwnerReplicaId);

/// <summary>
/// Derives spool state from the file locator and an optional lease.
/// </summary>
public static class SpoolStateResolver
{
    public static SpoolState Resolve(
        Guid assetId,
        ISpoolFileLocator locator,
        SpoolLease? lease)
    {
        var status = ResolveStatus(assetId, locator, lease);
        var filePath = locator.GetSpoolPath(assetId);

        return new SpoolState(
            Status: status,
            FilePath: filePath,
            OwnerReplicaId: lease?.OwnerReplicaId);
    }

    public static SpoolStatus ResolveStatus(
        Guid assetId,
        ISpoolFileLocator locator,
        SpoolLease? lease)
    {
        var fileExists = locator.IsSpooled(assetId);

        // File present — spool is complete regardless of lease state.
        // A lease may linger until the service releases it; that is not
        // our concern here.
        if (fileExists && lease is not null)
            return SpoolStatus.Downloading;

        // No file, but someone holds the slot — download is in progress.
        if (fileExists)
            return SpoolStatus.Completed;

        return SpoolStatus.NotExist;
    }
}
