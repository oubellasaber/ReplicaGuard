using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ReplicaGuard.Application.Abstractions.Common;
using ReplicaGuard.Domain.Abstractions;
using Sieve.Models;
using Sieve.Services;

namespace ReplicaGuard.Infrastructure.Filtering;

public class GridQueryExecutor : IGridQueryExecutor
{
    private readonly ISieveProcessor _sieveProcessor;

    public GridQueryExecutor(ISieveProcessor sieveProcessor)
    {
        _sieveProcessor = sieveProcessor;
    }

    public async Task<PagedList<TDto>> ToPagedListAsync<TEntity, TDto>(
        IQueryable<TEntity> query,
        ResourceParameters parameters,
        Expression<Func<TEntity, TDto>> projection,
        Expression<Func<TEntity, bool>>? preFilter = null,
        Expression<Func<TEntity, bool>>? customSearch = null,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        // 0. Domain pre-filters (e.g. computed Status)
        if (preFilter != null)
        {
            query = query.Where(preFilter);
        }

        // 1. Broad Keyword Search
        if (customSearch != null && !string.IsNullOrWhiteSpace(parameters.Search))
        {
            query = query.Where(customSearch);
        }

        // 2. Map pure Application parameters to SieveModel
        var sieveModel = new SieveModel
        {
            Filters = parameters.Filters,
            Sorts = parameters.Sorts,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        };

        // 3. Apply Filtering & Sorting (Count before pagination)
        var filteredQuery = _sieveProcessor.Apply(sieveModel, query, applyPagination: false);
        var totalCount = await filteredQuery.CountAsync(cancellationToken);

        // 4. Apply Pagination
        var pagedQuery = _sieveProcessor.Apply(sieveModel, filteredQuery, applyFiltering: false, applySorting: false);

        // 5. Database-level DTO Projection
        var items = await pagedQuery
            .Select(projection)
            .ToListAsync(cancellationToken);

        return new PagedList<TDto>(items, totalCount, parameters.Page, parameters.PageSize);
    }
}
