namespace ReplicaGuard.Infrastructure.Spool;

public interface ISpoolLeaseService
{
    Task<SpoolLease?> TryAcquireAsync(Guid assetId, Guid replicaId, TimeSpan ttl, CancellationToken ct);
    Task<bool> RenewAsync(Guid assetId, Guid replicaId, TimeSpan ttl, CancellationToken ct);
    Task<bool> ReleaseAsync(Guid assetId, Guid replicaId, CancellationToken ct);
    Task<SpoolLease?> GetAsync(Guid assetId, CancellationToken ct);
}
