using Microsoft.EntityFrameworkCore;
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
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.AssetId == assetId, ct);

            if (entity is null)
                return null;

            return new SpoolLease(
                entity.AssetId,
                entity.OwnerReplicaId,
                entity.ExpiresAtUtc
            );
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

        public async Task<bool> RenewAsync(
            Guid assetId,
            Guid replicaId,
            TimeSpan ttl,
            CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var entity = await _dbContext.Set<SpoolLease>()
                .FirstOrDefaultAsync(x => x.AssetId == assetId, ct);

            if (entity is null)
                return false;

            if (entity.OwnerReplicaId != replicaId)
                return false;

            entity.ExpiresAtUtc = now.Add(ttl);
            entity.Version++;

            try
            {
                await _dbContext.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                return false;
            }
        }

        public async Task<bool> ReleaseAsync(
            Guid assetId,
            Guid replicaId,
            CancellationToken ct)
        {
            var entity = await _dbContext.Set<SpoolLease>()
                .FirstOrDefaultAsync(x => x.AssetId == assetId, ct);

            if (entity is null || entity.OwnerReplicaId != replicaId)
                return false;

            entity.Version++;

            _dbContext.Set<SpoolLease>().Remove(entity);

            try
            {
                await _dbContext.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                return false;
            }
        }
    }
}
