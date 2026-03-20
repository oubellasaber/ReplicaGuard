using Microsoft.Extensions.Logging;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Credentials;
using ReplicaGuard.Core.Domain.Hoster;
using ReplicaGuard.Core.Domain.Replication;

namespace ReplicaGuard.Application.Assets.CreateAsset;

public sealed class CreateAssetCommandHandler(
    IAssetRepository assets,
    IHosterRepository hosters,
    IHosterCredentialsRepository credentials,
    IUserContext userContext,
    IUnitOfWork uow,
    ILogger<CreateAssetCommandHandler> logger)
        : ICommandHandler<CreateAssetCommand, CreateAssetResponse>
{
    public async Task<Result<CreateAssetResponse>> Handle(
        CreateAssetCommand request,
        CancellationToken cancellationToken)
    {
        Guid userId = userContext.UserId;

        // 1. Validate file name
        Result<FileName> fileNameResult = FileName.Create(request.FileName);
        if (fileNameResult.IsFailure)
            return Result.Failure<CreateAssetResponse>(fileNameResult.Error);

        // 2. Validate all hosters exist and user has synced credentials for each
        foreach (Guid hosterId in request.HosterIds)
        {
            Hoster? hoster = await hosters.GetByIdAsync(hosterId, cancellationToken);
            if (hoster == null)
                return Result.Failure<CreateAssetResponse>(HosterErrors.NotFound(hosterId));

            var creds = await credentials.FindByUserAndHosterAsync(
                userId, hosterId, cancellationToken);

            if (creds == null)
                return Result.Failure<CreateAssetResponse>(AssetErrors.MissingCredentials(hosterId));

            if (creds.SyncStatus != CredentialsSyncStatus.Synced)
                return Result.Failure<CreateAssetResponse>(AssetErrors.CredentialsNotSynced(hosterId));
        }

        // 3. Create asset — detect source type automatically
        Result<Asset> assetResult = IsUrl(request.Source)
            ? Asset.CreateFromRemoteUrl(userId, request.Source, fileNameResult.Value)
            : Asset.CreateFromLocalPath(userId, request.Source, fileNameResult.Value);

        if (assetResult.IsFailure)
            return Result.Failure<CreateAssetResponse>(assetResult.Error);

        Asset asset = assetResult.Value;

        // 4. Add replicas
        foreach (Guid hosterId in request.HosterIds)
        {
            Result<Replica> replicaResult = asset.AddReplica(hosterId);
            if (replicaResult.IsFailure)
                return Result.Failure<CreateAssetResponse>(replicaResult.Error);
        }

        // 5. Persist — domain event triggers the upload pipeline
        assets.Add(asset);
        await uow.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Asset {AssetId} created with {ReplicaCount} replicas for user {UserId}",
            asset.Id, request.HosterIds.Count, userId);

        return Result.Success(new CreateAssetResponse(
            asset.Id,
            asset.FileName.Value,
            asset.State.ToString().ToLowerInvariant(),
            asset.Replicas.Count,
            asset.CreatedAtUtc));
    }

    private static bool IsUrl(string source) =>
        Uri.TryCreate(source, UriKind.Absolute, out Uri? uri) &&
        uri.Scheme is "http" or "https";
}
