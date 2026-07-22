using Microsoft.EntityFrameworkCore;

namespace ReplicaGuard.Application.Abstractions.Data;

public interface IApplicationDbContext
{
    DbSet<TEntity> Set<TEntity>()
        where TEntity : class;
}
