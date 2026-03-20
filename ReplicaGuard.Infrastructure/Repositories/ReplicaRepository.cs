using Microsoft.EntityFrameworkCore;
using ReplicaGuard.Core.Domain.Replication;
using ReplicaGuard.Infrastructure.Persistence;

namespace ReplicaGuard.Infrastructure.Repositories;

internal sealed class ReplicaRepository : Repository<Replica>, IReplicaRepository
{
    public ReplicaRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<List<Replica>> GetPendingReplicasAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        return await DbContext
            .Set<Replica>()
            .Where(r => r.State == ReplicaState.Pending)
            .OrderBy(r => r.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }
}
