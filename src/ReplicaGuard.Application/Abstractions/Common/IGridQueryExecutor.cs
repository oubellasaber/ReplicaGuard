using System.Linq.Expressions;
using ReplicaGuard.Domain.Abstractions;

namespace ReplicaGuard.Application.Abstractions.Common;

public interface IGridQueryExecutor
{
    Task<PagedList<TDto>> ToPagedListAsync<TEntity, TDto>(
        IQueryable<TEntity> query,
        ResourceParameters parameters,
        Expression<Func<TEntity, TDto>> projection,
        Expression<Func<TEntity, bool>>? preFilter = null,
        Expression<Func<TEntity, bool>>? customSearch = null,
        CancellationToken cancellationToken = default)
        where TEntity : class;
}
