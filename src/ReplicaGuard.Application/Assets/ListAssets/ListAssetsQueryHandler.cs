using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Replication;

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
                a.Status.ToString().ToLowerInvariant(),
                a.SizeBytes,
                a.Replicas.Count,
                a.CreatedAtUtc,
                a.UpdatedAtUtc))
            .ToList();

        return Result.Success(response);
    }
}
