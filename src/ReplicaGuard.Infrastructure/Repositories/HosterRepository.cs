using Microsoft.EntityFrameworkCore;
using ReplicaGuard.Core.Hosters;
using ReplicaGuard.Infrastructure.Persistence;

namespace ReplicaGuard.Infrastructure.Repositories;

internal class HosterRepository : IHosterRepository
{
    private readonly ApplicationDbContext DbContext;

    public HosterRepository(ApplicationDbContext dbContext)
    {
        DbContext = dbContext;
    }

    public async Task<Hoster?> GetByIdAsync(HosterCode id, CancellationToken ctn)
    {
        return await DbContext
            .Set<Hoster>()
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == id, ctn);
    }

    public async Task<List<Hoster>> GetAllAsync(CancellationToken ctn = default)
    {
        return await DbContext
            .Set<Hoster>()
            .AsNoTracking()
            .OrderBy(h => h.DisplayName)
            .ToListAsync(ctn);
    }
}
