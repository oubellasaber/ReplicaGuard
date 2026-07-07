using Microsoft.EntityFrameworkCore;
using ReplicaGuard.Domain.Replication;
using ReplicaGuard.Domain.Users;
using ReplicaGuard.Infrastructure.Persistence;

namespace ReplicaGuard.Infrastructure.Repositories;

internal sealed class AssetRepository : Repository<Asset>, IAssetRepository
{
    public AssetRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public async new Task<Asset?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext
            .Set<Asset>()
            .Include(a => a.Replicas)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<Asset?> GetByIdWithReplicasAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbContext
            .Set<Asset>()
            .Include(a => a.Replicas)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
    }

    public async Task<List<Asset>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbContext
            .Set<Asset>()
            .Where(x => x.UserId == userId)
            .Include(a => a.Replicas)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<Asset?> GetByReplicaIdWithReplicasAsync(Guid replicaId, CancellationToken cancellationToken = default)
    {
        return await DbContext
            .Set<Asset>()
            .Include(a => a.Replicas)
            .Where(x => x.Replicas.Any(r => r.Id == replicaId))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
