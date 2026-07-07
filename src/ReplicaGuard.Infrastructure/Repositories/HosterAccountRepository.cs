using Microsoft.EntityFrameworkCore;
using ReplicaGuard.Domain.HosterAccounts;
using ReplicaGuard.Infrastructure.Persistence;

namespace ReplicaGuard.Infrastructure.Repositories;

internal sealed class HosterAccountRepository : Repository<HosterAccount>, IHosterAccountRepository
{
    public HosterAccountRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public new async Task<HosterAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<HosterAccount>()
            .Where(a => a.Id == id)
            .Include(a => a.Hoster)
            .Include(a => a.Identities)
                .ThenInclude(i => i.SecretSet)
                    .ThenInclude(s => s.Secrets)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<HosterAccount>> GetAccountsByIds(Guid userId, IEnumerable<Guid> accounts, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<HosterAccount>()
            .Where(a => a.UserId == userId && accounts.Contains(a.Id))
            .Include(a => a.Hoster)
            .Include(a => a.Identities)
                .ThenInclude(i => i.SecretSet)
                    .ThenInclude(s => s.Secrets)
            .ToListAsync(cancellationToken);
    }

    public async Task<HosterAccount?> GetByIdentityIdAsync(Guid identityId, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<HosterAccount>()
            .Where(a => a.Identities.Any(i => i.Id == identityId))
            .Include(a => a.Hoster)
            .Include(a => a.Identities)
                .ThenInclude(i => i.SecretSet)
                    .ThenInclude(s => s.Secrets)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
