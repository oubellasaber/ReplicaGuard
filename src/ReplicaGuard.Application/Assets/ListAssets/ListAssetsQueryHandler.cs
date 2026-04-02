using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Replication;

namespace ReplicaGuard.Application.Assets.ListAssets;

public sealed class ListAssetsQueryHandler(
    IAssetRepository assets,
    IUserContext userContext)
        : IQueryHandler<ListAssetsQuery, List<AssetSummaryResponse>>
{
    public async Task<Result<List<AssetSummaryResponse>>> Handle(
        ListAssetsQuery request,
        CancellationToken cancellationToken)
    {
        List<Asset> userAssets = await assets.GetByUserIdAsync(
            userContext.UserId, cancellationToken);

        List<AssetSummaryResponse> response = userAssets
            .Select(a => new AssetSummaryResponse(
                a.Id,
                a.FileName.Value,
                a.State.ToString().ToLowerInvariant(),
                a.SizeBytes,
                a.Replicas.Count,
                a.Replicas.Count(r => r.State == ReplicaState.Completed),
                a.Replicas.Count(r => r.State == ReplicaState.Failed),
                a.CreatedAtUtc))
            .ToList();

        return Result.Success(response);
    }
}
