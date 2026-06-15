using Microsoft.EntityFrameworkCore;
using ReplicaGuard.Application.Replication.UploadReplica.Spooling;
using ReplicaGuard.Infrastructure.Persistence;

namespace ReplicaGuard.Infrastructure.Spool
{
    /// <summary>
    /// SQL-backed distributed lease for spool ownership.
    /// Ensures only one replica prepares the spool at a time.
    /// Uses optimistic concurrency to prevent race conditions.
    /// </summary>
    public sealed class SqlSpoolLeaseService : ISpoolLeaseService
    {
        private readonly ApplicationDbContext _dbContext;

        public SqlSpoolLeaseService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<SpoolLease?> GetAsync(Guid assetId, CancellationToken ct)
        {
            var entity = await _dbContext.Set<SpoolLease>()
                .FirstOrDefaultAsync(x => x.AssetId == assetId, ct);

            return entity;
        }

        public async Task<SpoolLease?> TryAcquireAsync(
            Guid assetId,
            Guid replicaId,
            TimeSpan ttl,
            CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var expires = now.Add(ttl);

            var entity = await _dbContext.Set<SpoolLease>()
                .FirstOrDefaultAsync(x => x.AssetId == assetId, ct);

            if (entity is null)
            {
                // Try to create a new lease
                entity = new SpoolLease(assetId, replicaId, expires);

                _dbContext.Set<SpoolLease>().Add(entity);

                try
                {
                    await _dbContext.SaveChangesAsync(ct);
                    return new SpoolLease(assetId, replicaId, expires);
                }
                catch (DbUpdateException)
                {
                    // Someone else created it first
                    return null;
                }
            }

            // Existing lease: check if expired
            if (entity.ExpiresAtUtc > now)
            {
                // Lease is still valid — cannot acquire
                return null;
            }

            // Try to take over expired lease
            entity.OwnerReplicaId = replicaId;
            entity.ExpiresAtUtc = expires;
            entity.Version++;

            try
            {
                await _dbContext.SaveChangesAsync(ct);
                return new SpoolLease(assetId, replicaId, expires);
            }
            catch (DbUpdateConcurrencyException)
            {
                return null;
            }
        }

        public void Renew(SpoolLease lease, TimeSpan ttl)
        {
            var now = DateTime.UtcNow;
            lease.ExpiresAtUtc = now.Add(ttl);
            lease.Version++;
        }

        public void Release(SpoolLease lease)
        {
            _dbContext.Set<SpoolLease>().Remove(lease);
        }

        public async Task ReleaseForAsset(Guid assetId)
        {
            await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM replicaguard.spool_leases WHERE asset_id = {assetId}");
        }
    }
}
