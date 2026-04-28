using ReplicaGuard.Core.Domain.Replication.Planner;
using ReplicaGuard.Infrastructure.Hosters;
using ReplicaGuard.Infrastructure.Spool;

public static class SpoolStateFactory
{
    public static SpoolState Create(
        Guid assetId,
        FileFetcher fileFetcher,
        SpoolLease? lease)
    {
        var status = GetStatus(assetId, fileFetcher, lease);
        var filePath = fileFetcher.GetSpoolPath(assetId);

        return new SpoolState(
            Status: status,
            FilePath: filePath,
            OwnerReplicaId: lease?.OwnerReplicaId
        );
    }

    public static SpoolStatus GetStatus(
        Guid assetId,
        FileFetcher fileFetcher,
        SpoolLease? lease)
    {
        var fileExists = fileFetcher.IsSpooled(assetId);

        if (!fileExists && lease is null)
            return SpoolStatus.NotExist;

        if (!fileExists && lease is not null)
            return SpoolStatus.Downloading;

        return SpoolStatus.Completed;
    }
}
