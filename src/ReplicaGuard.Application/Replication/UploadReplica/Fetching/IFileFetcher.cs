using ReplicaGuard.Application.Replication.UploadReplica.Spooling;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Replication;

namespace ReplicaGuard.Application.Replication.UploadReplica.Fetching;

/// <summary>
/// Downloads a remote file and writes it to the spool store.
/// </summary>
public interface IFileFetcher
{
    Task<Result<SpooledFile>> DownloadAsync(
        Guid assetId,
        RemoteFileSource source,
        CancellationToken ct = default);
}

public sealed class FileFetcherOptions
{
    public static readonly string SectionName = "Spool";
    public required string SpoolDirectory { get; init; }
}
