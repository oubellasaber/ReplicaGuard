using Microsoft.EntityFrameworkCore;
using ReplicaGuard.Core.Domain.Credentials;
using ReplicaGuard.Infrastructure.Persistence;

namespace ReplicaGuard.Infrastructure.Repositories;
internal class HosterCredentialsRepository : Repository<HosterCredentials>, IHosterCredentialsRepository
{
    public HosterCredentialsRepository(ApplicationDbContext dbContext) 
        : base(dbContext)
    {
    }

    public async Task<HosterCredentials?> FindByUserAndHosterAsync(Guid userId, Guid hosterId, CancellationToken ct)
    {
        return await DbContext
            .Set<HosterCredentials>()
            .FirstOrDefaultAsync(hc => hc.UserId == userId && hc.HosterId == hosterId, ct);
    }
}
