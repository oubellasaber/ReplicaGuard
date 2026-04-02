using Microsoft.EntityFrameworkCore;
using ReplicaGuard.Core.Domain.Hoster;
using ReplicaGuard.Infrastructure.Persistence;

namespace ReplicaGuard.Infrastructure.Repositories;

internal class HosterRepository : Repository<Hoster>, IHosterRepository
{
    public HosterRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public new async Task<Hoster?> GetByIdAsync(Guid id, CancellationToken ctn)
    {
        return await DbContext
            .Set<Hoster>()
            .AsNoTracking()
            .Include(h => h.Requirements)
            .FirstOrDefaultAsync(h => h.Id == id, ctn);
    }

    public async Task<List<Hoster>> GetAllAsync(CancellationToken ctn = default)
    {
        return await DbContext
            .Set<Hoster>()
            .AsNoTracking()
            .Include(h => h.Requirements)
            .OrderBy(h => h.DisplayName)
            .ToListAsync(ctn);
    }

    public async Task<List<string>> GetAllCodesAsync(CancellationToken ctn = default)
    {
        return await DbContext
            .Set<Hoster>()
            .AsNoTracking()
            .Select(h => h.Code)
            .ToListAsync(ctn);
    }
}
