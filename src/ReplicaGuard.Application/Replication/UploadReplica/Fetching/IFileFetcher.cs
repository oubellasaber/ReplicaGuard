using ReplicaGuard.Application.Replication.UploadReplica.Spooling;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Capabilities;
using ReplicaGuard.Domain.Replication;

namespace ReplicaGuard.Application.Replication.UploadReplica.Fetching;

/// <summary>
/// Downloads a remote file and writes it to the spool store.
/// </summary>
public interface IFileFetcher
{
    Task<Result<SpooledFile>> DownloadAsync(
        Guid assetId,
        RemoteFileSource source,
        Action<TransferProgress>? onProgress = null,
        CancellationToken ct = default);
}

public sealed class FileFetcherOptions
{
    public static readonly string SectionName = "Spool";
    public required string SpoolDirectory { get; init; }
}
