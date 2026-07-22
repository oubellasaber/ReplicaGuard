using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Abstractions.Common;
using ReplicaGuard.Application.Abstractions.Data;
using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Replication;

namespace ReplicaGuard.Application.Assets.ListAssets;

public sealed class ListAssetsQueryHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext,
    IGridQueryExecutor gridQueryExecutor)
        : IQueryHandler<ListAssetsQuery, PagedList<AssetSummaryResponse>>
{
    private static readonly Regex StatusFilterPattern = new(
        @"(?:^|,)status\s*(==|!=)\s*(\w+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public async Task<Result<PagedList<AssetSummaryResponse>>> Handle(
        ListAssetsQuery request,
        CancellationToken cancellationToken)
    {
        var p = request.Parameters;

        // Extract status filter from Sieve filters and build a pre-filter expression
        var (preFilter, cleanFilters) = BuildStatusFilter(p.Filters);

        var cleanParams = p with
        {
            Filters = cleanFilters,
            Sorts = string.IsNullOrWhiteSpace(p.Sorts) ? $"-{nameof(Asset.CreatedAtUtc)}" : p.Sorts
        };

        var query = dbContext.Set<Asset>()
            .Where(a => a.UserId == userContext.UserId)
            .AsNoTracking();

        var pagedProjections = await gridQueryExecutor.ToPagedListAsync(
            query,
            cleanParams,
            x => new
            {
                x.Id,
                FileName = x.FileName.Value,
                x.SizeBytes,
                TotalReplicas = x.Replicas.Count,
                x.CreatedAtUtc,
                x.UpdatedAtUtc,

                NoReplicasOrAllPending = !x.Replicas.Any() || x.Replicas.All(r => r.Status == ReplicaStatus.Pending),
                AllCompleted = x.Replicas.All(r => r.Status == ReplicaStatus.Completed),
                AllFailed = x.Replicas.All(r => r.Status == ReplicaStatus.Failed)
            },
            preFilter: preFilter,
            customSearch: string.IsNullOrWhiteSpace(p.Search)
                ? null
                : x => x.FileName.Value.ToLower().Contains(p.Search.Trim().ToLower()),
            cancellationToken
        );

        var responses = pagedProjections.Items.Select(x =>
        {
            var status = x.NoReplicasOrAllPending ? AssetStatus.Created
                       : x.AllCompleted ? AssetStatus.Completed
                       : x.AllFailed ? AssetStatus.Failed
                       : AssetStatus.InProgress;

            return new AssetSummaryResponse(
                x.Id,
                x.FileName,
                status.ToString().ToLowerInvariant(),
                x.SizeBytes,
                x.TotalReplicas,
                x.CreatedAtUtc,
                x.UpdatedAtUtc
            );
        }).ToList();

        return Result.Success(new PagedList<AssetSummaryResponse>(
            responses,
            pagedProjections.TotalCount,
            pagedProjections.CurrentPage,
            pagedProjections.PageSize));
    }

    private static (Expression<Func<Asset, bool>>? filter, string? remainingFilters) BuildStatusFilter(
        string? filters)
    {
        if (string.IsNullOrWhiteSpace(filters))
            return (null, null);

        var match = StatusFilterPattern.Match(filters);
        if (!match.Success)
            return (null, filters);

        if (!Enum.TryParse<AssetStatus>(match.Groups[2].Value, ignoreCase: true, out var status))
            return (null, filters);

        var op = match.Groups[1].Value;
        var remaining = filters[..match.Index] + filters[(match.Index + match.Length)..];
        if (string.IsNullOrWhiteSpace(remaining))
            remaining = null;

        var expression = BuildStatusExpression(status, op);
        return (expression, remaining);
    }

    private static Expression<Func<Asset, bool>> BuildStatusExpression(AssetStatus status, string op)
    {
        Expression<Func<Asset, bool>> expr = status switch
        {
            AssetStatus.Created => a =>
                !a.Replicas.Any() ||
                a.Replicas.All(r => r.Status == ReplicaStatus.Pending),

            AssetStatus.Completed => a =>
                a.Replicas.Any() &&
                a.Replicas.All(r => r.Status == ReplicaStatus.Completed),

            AssetStatus.Failed => a =>
                a.Replicas.Any() &&
                a.Replicas.All(r => r.Status == ReplicaStatus.Failed),

            AssetStatus.InProgress => a =>
                a.Replicas.Any() &&
                a.Replicas.Any(r => r.Status != ReplicaStatus.Pending) &&
                a.Replicas.Any(r => r.Status != ReplicaStatus.Completed) &&
                a.Replicas.Any(r => r.Status != ReplicaStatus.Failed),

            _ => a => true
        };

        if (op == "!=")
        {
            var parameter = Expression.Parameter(typeof(Asset), "a");
            var body = Expression.Not(Expression.Invoke(expr, parameter));
            return Expression.Lambda<Func<Asset, bool>>(body, parameter);
        }

        return expr;
    }
}
