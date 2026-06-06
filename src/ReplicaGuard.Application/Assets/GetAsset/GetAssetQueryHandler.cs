using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Replication;

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
        Asset? asset = await assets.GetByIdWithReplicasAsync(
            request.AssetId,
            cancellationToken);

        if (asset is null || asset.UserId != userContext.UserId)
        {
            return Result.Failure<GetAssetResponse>(
                ReplicationErrors.AssetNotFound(request.AssetId));
        }

        List<ReplicaResponse> replicas = asset.Replicas
            .OrderBy(r => r.CreatedAtUtc)
            .Select(r => new ReplicaResponse(
                r.Id,
                r.HosterId,
                r.Status.ToString().ToLowerInvariant(),
                r.Link?.ToString(),
                r.CreatedAtUtc))
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
