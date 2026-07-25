using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Capabilities;
using ReplicaGuard.Domain.Hosters;
using ReplicaGuard.Domain.Replication;

namespace ReplicaGuard.Application.Replicas.GenerateDownloadUrl;

internal sealed class GenerateDownloadUrlCommandHandler(
    IAssetRepository assets,
    IHosterRepository hosters,
    IUserContext userContext,
    ICapabilityFactory capabilityFactory)
    : ICommandHandler<GenerateDownloadUrlCommand, GenerateDownloadUrlResponse>
{
    public async Task<Result<GenerateDownloadUrlResponse>> Handle(
        GenerateDownloadUrlCommand request,
        CancellationToken cancellationToken)
    {
        var userId = userContext.UserId;

        var asset = await assets.GetByIdWithReplicasAsync(request.AssetId, userId, cancellationToken);
        if (asset is null)
            return Result.Failure<GenerateDownloadUrlResponse>(
                ReplicationErrors.AssetNotFound(request.AssetId));

        var replica = asset.Replicas.FirstOrDefault(r => r.Id == request.ReplicaId);
        if (replica is null)
            return Result.Failure<GenerateDownloadUrlResponse>(
                ReplicationErrors.ReplicaNotFound(request.ReplicaId));

        if (replica.Status != ReplicaStatus.Completed)
            return Result.Failure<GenerateDownloadUrlResponse>(
                GenerateDownloadUrlErrors.ReplicaNotCompleted(request.ReplicaId));

        if (replica.Link is null)
            return Result.Failure<GenerateDownloadUrlResponse>(
                GenerateDownloadUrlErrors.MissingLink(request.ReplicaId));

        var hoster = await hosters.GetByIdAsync(replica.HosterId, cancellationToken);
        if (hoster is null)
            return Result.Failure<GenerateDownloadUrlResponse>(
                HosterErrors.NotFound(replica.HosterId));

        var handler = capabilityFactory.Get<IGenerateDownloadUrlCapabilityHandler>(hoster.Code);
        if (handler is null)
            return Result.Failure<GenerateDownloadUrlResponse>(
                new Error("GenerateDownloadUrl.HandlerNotFound",
                    $"No download URL handler for hoster {hoster.Code.ToFriendlyString()}."));

        var downloadRequest = new DownloadFileRequest(replica.Link);
        var result = await handler.HandleAsync(downloadRequest, cancellationToken);

        if (result.IsFailure)
            return Result.Failure<GenerateDownloadUrlResponse>(result.Error);

        return Result.Success(new GenerateDownloadUrlResponse(
            result.Value.DownloadUrl,
            result.Value.RequiredHeaders));
    }
}