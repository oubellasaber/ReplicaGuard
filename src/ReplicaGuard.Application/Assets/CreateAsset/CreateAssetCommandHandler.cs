using Microsoft.Extensions.Logging;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Abstractions.Clock;
using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.HosterAccounts;
using ReplicaGuard.Domain.Hosters;
using ReplicaGuard.Domain.Replication;

namespace ReplicaGuard.Application.Assets.CreateAsset;

internal sealed class CreateAssetCommandHandler(
    IUserContext userContext,
    IHosterDefinitionResolver resolver,
    IHosterAccountRepository accountRepository,
    IAssetRepository assets,
    IUnitOfWork uow,
    IDateTimeProvider clock,
    ILogger<CreateAssetCommandHandler> logger)
        : ICommandHandler<CreateAssetCommand, CreateAssetResponse>
{
    public async Task<Result<CreateAssetResponse>> Handle(
        CreateAssetCommand request,
        CancellationToken ct)
    {
        Guid userId = userContext.UserId;
        var now = clock.UtcNow;

        // 1. Validate file name
        var fileNameResult = ValidateFileName(request.FileName);
        if (fileNameResult.IsFailure)
            return Result.Failure<CreateAssetResponse>(fileNameResult.Error);

        // 2. Load accounts by HosterAccountId
        var accountsResult = await LoadAccounts(request.HosterAccountIds, userId, ct);
        if (accountsResult.IsFailure)
            return Result.Failure<CreateAssetResponse>(accountsResult.Error);

        var accounts = accountsResult.Value;

        // 3. Determine capability
        var capability = DetermineCapability(request.Source);

        // 4. Validate capability per hoster
        var capabilityResult = ValidateCapabilityForAllHosters(
            accounts,
            capability,
            request.Source);
        if (capabilityResult.IsFailure)
            return Result.Failure<CreateAssetResponse>(capabilityResult.Error);

        // 5. Create asset with replicas
        var replicas = accounts
            .Select(a => (a.HosterId, (Guid?)a.Id))
            .ToList();

        var assetResult = CreateAsset(
            userId,
            request.Source,
            fileNameResult.Value,
            replicas,
            request.AssetId,
            request.BaseDirectory);
        if (assetResult.IsFailure)
            return Result.Failure<CreateAssetResponse>(assetResult.Error);

        // 7. Persist
        var asset = assetResult.Value;
        assets.Add(asset);
        await uow.SaveChangesAsync(ct);

        logger.LogInformation(
            "Asset {AssetId} created with {ReplicaCount} replicas for user {UserId}",
            asset.Id, request.HosterAccountIds.Count(), userId);

        return Result.Success(new CreateAssetResponse(
            asset.Id,
            asset.FileName.Value,
            asset.Status.ToString().ToLowerInvariant(),
            asset.Replicas.Count,
            asset.CreatedAtUtc));
    }

    private static Result<FileName> ValidateFileName(string fileName)
        => FileName.Create(fileName);

    private async Task<Result<List<HosterAccount>>> LoadAccounts(
        IEnumerable<Guid> accountIds,
        Guid userId,
        CancellationToken ct)
    {
        var accounts = await accountRepository.GetAccountsByIds(userId, accountIds, ct);

        if (accounts.Count() != accountIds.Count())
        {
            var missing = accountIds
                .Where(id => !accounts.Any(a => a.Id == id));

            return Result.Failure<List<HosterAccount>>(
                HosterAccountErrors.NotFound(missing.First()));
        }

        return accounts.ToList();
    }

    private static CapabilityCode DetermineCapability(string source)
        => IsUrl(source)
            ? CapabilityCode.RemoteFileUpload
            : CapabilityCode.LocalFileUpload;

    private Result ValidateCapabilityForAllHosters(
        List<HosterAccount> accounts,
        CapabilityCode capability,
        string source)
    {
        bool isRemote = IsUrl(source);

        foreach (var account in accounts)
        {
            var def = resolver.Resolve(account.HosterCode);

            // 1. Resolve capability requirement
            var requirement = def.GetRequirement(capability);

            // Remote fallback: if remote not supported, try local
            if (isRemote && requirement is null)
                requirement = def.GetRequirement(CapabilityCode.LocalFileUpload);

            if (requirement is null)
            {
                return Result.Failure(
                    HosterErrors.CapabilityNotSupported(def.Code, capability));
            }

            // 2. Filter verified identities
            var verified = account.Identities
                .Where(i => i.Status == IdentityVerificationStatus.Verified)
                .ToList();

            // 3. OR-of-ANDs capability requirement
            if (!requirement.IsSatisfiedBy(verified))
            {
                return Result.Failure(
                    HosterAccountErrors.RequiredIdentitiesNotSatisfied(
                        requirement,
                        def.Code,
                        capability));
            }
        }

        return Result.Success();
    }

    private static Result<Asset> CreateAsset(
        Guid userId,
        string source,
        FileName fileName,
        IEnumerable<(Guid hosterId, Guid? accountId)> replicas,
        Guid? assetId = null,
        string? baseDirectory = null)
    {
        if (IsUrl(source))
        {
            return Asset.CreateFromRemoteUrl(userId, source, fileName, replicas);
        }

        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            throw new ArgumentException("Base directory must be provided for local file uploads.", nameof(baseDirectory));
        }

        return Asset.CreateFromLocalPath(userId, baseDirectory, source, fileName, replicas, assetId);
    }

    private static bool IsUrl(string source)
        => Uri.TryCreate(source, UriKind.Absolute, out var uri)
        && (uri.Scheme == "http" || uri.Scheme == "https");
}
