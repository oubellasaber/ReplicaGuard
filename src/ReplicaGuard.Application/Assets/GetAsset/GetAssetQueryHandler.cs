using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Hosters;
using ReplicaGuard.Core.Replication;

namespace ReplicaGuard.Application.Assets.GetAsset;

public sealed class GetAssetQueryHandler(
    IAssetRepository assets,
    IUserContext userContext)
        : IQueryHandler<GetAssetQuery, GetAssetResponse>
{
    public async Task<Result<GetAssetResponse>> Handle(
        GetAssetQuery request,
        CancellationToken cancellationToken)
    {
        var userId = userContext.UserId;
        Asset? asset = await assets.GetByIdWithReplicasAsync(
            request.AssetId,
            userId,
            cancellationToken);

        if (asset is null)
        {
            return Result.Failure<GetAssetResponse>(
                ReplicationErrors.AssetNotFound(request.AssetId));
        }

        List<ReplicaResponse> replicas = asset.Replicas
            .OrderByDescending(r => r.Status)
            .Select(r => new ReplicaResponse(
                r.Id,
                r.HosterId.ToFriendlyString(),
                r.HosterAccountId,
                r.Status.ToString().ToLowerInvariant(),
                r.Link?.ToString(),
                r.CreatedAtUtc,
                r.UpdatedAtUtc))
            .ToList();

        return Result.Success(new GetAssetResponse(
            asset.Id,
            asset.FileName.Value,
            asset.Status.ToString().ToLowerInvariant(),
            asset.SizeBytes,
            asset.CreatedAtUtc,
            asset.UpdatedAtUtc,
            replicas));
    }
}
