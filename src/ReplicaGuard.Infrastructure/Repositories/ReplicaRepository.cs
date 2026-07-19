using Microsoft.AspNetCore.Connections;
using Microsoft.EntityFrameworkCore;
using ReplicaGuard.Application.Abstractions.Data;
using ReplicaGuard.Application.Replication.UploadReplica.Spooling;
using ReplicaGuard.Domain.Replication;
using ReplicaGuard.Infrastructure.Persistence;

namespace ReplicaGuard.Infrastructure.Repositories;

internal sealed class ReplicaRepository : Repository<Replica>, IReplicaRepository
{
    public ReplicaRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<MarkWaitingResult> TryMarkWaitingIfDownloaderStillActive(
        Guid assetId,
        Guid replicaId,
        CancellationToken ct)
    {
        // 1) Load the lease row (no tracking)
        var lease = await DbContext
            .Set<SpoolLease>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AssetId == assetId, ct);

        // No lease row => downloader finished normally
        if (lease is null || lease.OwnerReplicaId == Guid.Empty)
            return MarkWaitingResult.AlreadyCompleted;

        var downloaderId = lease.OwnerReplicaId;

        // 2) Atomic update: mark waiting ONLY if lease still has same owner
        var rows = await DbContext.Database.ExecuteSqlInterpolatedAsync(
            $@"
                UPDATE replicaguard.replicas
                SET status = {(int)ReplicaStatus.WaitingForPeer},
                    waiting_for_replica_id = {downloaderId},
                    updated_at_utc = NOW()
                WHERE id = {replicaId}
                  AND EXISTS (
                        SELECT 1
                        FROM replicaguard.spool_leases
                        WHERE asset_id = {assetId}
                          AND owner_replica_id = {downloaderId}
                          AND expires_at_utc > NOW()
                  );
            ", ct);

        if (rows == 1)
            return MarkWaitingResult.MarkedWaiting;

        // 3) Rare race: downloader disappeared between SELECT and UPDATE
        var leaseAfter = await DbContext
            .Set<SpoolLease>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AssetId == assetId, ct);

        // A) Downloader finished normally
        if (leaseAfter is null)
            return MarkWaitingResult.AlreadyCompleted;

        // B) Downloader disappeared unexpectedly => force retry
        return MarkWaitingResult.NoActiveDownloader;
    }

    public async Task<IReadOnlyList<Replica>> GetReplicasNearExpiryAsync(
        DateTime now,
        TimeSpan window,
        int batchSize,
        int recoveryBackoffMinutes,
        CancellationToken ct)
    {
        var cutoff = now.Add(window);

        return await DbContext.Set<Replica>()
            .Where(r =>
                r.Status == ReplicaStatus.Completed &&
                r.AvailabilityStatus != ReplicaAvailabilityStatus.Tombstoned &&
                r.AvailabilityStatus != ReplicaAvailabilityStatus.Processing &&

                // Backoff: skip if a recent recovery attempt is still cooling down
                (r.LastRecoveryAttemptAtUtc == null ||
                 r.LastRecoveryAttemptAtUtc.Value.AddMinutes(
                     recoveryBackoffMinutes * (r.RecoveryAttemptCount + 1)) <= now) &&

                // Need attention:
                (
                    // 1) Never checked
                    (r.AvailabilityStatus == ReplicaAvailabilityStatus.Unknown &&
                     r.PredictedExpiryAtUtc == null) ||

                    // 2) Healthy but approaching expiry
                    (r.AvailabilityStatus == ReplicaAvailabilityStatus.Healthy &&
                     r.PredictedExpiryAtUtc != null &&
                     r.PredictedExpiryAtUtc <= cutoff) ||

                    // 3) In danger — always check (backoff already applied above)
                    (r.AvailabilityStatus == ReplicaAvailabilityStatus.ExpiringSoon || r.AvailabilityStatus == ReplicaAvailabilityStatus.Expired)
                ))
            .OrderBy(r => r.PredictedExpiryAtUtc)
            .Take(batchSize)
            .ToListAsync(ct);
    }
}
