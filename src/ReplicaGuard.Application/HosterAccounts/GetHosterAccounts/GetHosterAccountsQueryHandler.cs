using Microsoft.EntityFrameworkCore;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Abstractions.Common;
using ReplicaGuard.Application.Abstractions.Data;
using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.HosterAccounts;

namespace ReplicaGuard.Application.HosterAccounts.GetHosterAccounts;

public sealed class GetHosterAccountsQueryHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext,
    IGridQueryExecutor gridQueryExecutor)
    : IQueryHandler<GetHosterAccountsQuery, PagedList<HosterAccountSummaryResponse>>
{
    public async Task<Result<PagedList<HosterAccountSummaryResponse>>> Handle(
        GetHosterAccountsQuery request,
        CancellationToken cancellationToken)
    {
        var p = new ResourceParameters 
        { 
            Sorts = $"-{nameof(HosterAccount.CreatedAtUtc)}", 
            Page = request.Parameters.Page, 
            PageSize = request.Parameters.PageSize 
        };

        var query = dbContext.Set<HosterAccount>()
            .Where(a => a.UserId == userContext.UserId)
            .Include(a => a.Hoster)
            .AsNoTracking();

        var paged = await gridQueryExecutor.ToPagedListAsync(
            query,
            p,
            a => new HosterAccountSummaryResponse(
                a.Id,
                a.Hoster.Code,
                a.Hoster.DisplayName,
                a.Alias,
                a.Description,
                a.Identities.Count,
                a.CreatedAtUtc,
                a.UpdatedAtUtc),
            customSearch: string.IsNullOrWhiteSpace(p.Search)
                ? null
                : a => a.Alias.ToLower().Contains(p.Search.Trim().ToLower()),
            cancellationToken: cancellationToken);

        return Result.Success(paged);
    }
}
