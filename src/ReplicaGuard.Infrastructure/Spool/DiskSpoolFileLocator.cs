using Microsoft.Extensions.Options;
using ReplicaGuard.Application.Replication.UploadReplica.Fetching;
using ReplicaGuard.Application.Replication.UploadReplica.Spooling;

namespace ReplicaGuard.Infrastructure.Spool;

/// <summary>
/// Resolves spool paths on local disk.
/// </summary>
internal sealed class DiskSpoolFileLocator : ISpoolFileLocator
{
    private readonly string _spoolDirectory;

    public DiskSpoolFileLocator(IOptions<FileFetcherOptions> options)
    {
        _spoolDirectory = options.Value.SpoolDirectory;
    }

    public string GetSpoolPath(Guid assetId, string fileName) =>
        Path.Combine(_spoolDirectory, $"spl_{assetId}_{fileName}");

    public string GetTempSpoolPath(Guid assetId, string fileName) =>
        Path.Combine(_spoolDirectory, $"spl_{assetId}_{fileName}.tmp");

    public bool IsSpooled(Guid assetId, string fileName) =>
        File.Exists(GetSpoolPath(assetId, fileName));
}
