namespace ReplicaGuard.Application.Replication.UploadReplica.Spooling;

/// <summary>
/// Queries the local spool store
/// </summary>
public interface ISpoolFileLocator
{
    string GetSpoolPath(Guid assetId);
    bool IsSpooled(Guid assetId);
}

public sealed record SpooledFile(string Path, long SizeBytes);
