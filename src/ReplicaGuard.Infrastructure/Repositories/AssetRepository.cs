using Microsoft.EntityFrameworkCore;
using ReplicaGuard.Core.Domain.Replication;
using ReplicaGuard.Infrastructure.Persistence;

namespace ReplicaGuard.Infrastructure.Repositories;

internal sealed class AssetRepository : Repository<Asset>, IAssetRepository
{
    public AssetRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<Asset?> GetByIdWithReplicasAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext
            .Set<Asset>()
            .Include(a => a.Replicas)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<List<Asset>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbContext
            .Set<Asset>()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }
}
