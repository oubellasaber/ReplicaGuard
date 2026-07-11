namespace ReplicaGuard.Application.Replication.UploadReplica.Spooling;

/// <summary>
/// Queries the local spool store
/// </summary>
public interface ISpoolFileLocator
{
    string GetSpoolPath(Guid assetId, string fileName);
    string GetTempSpoolPath(Guid assetId, string fileName);
    bool IsSpooled(Guid assetId, string fileName);
}

public sealed record SpooledFile(string Path, long SizeBytes);
