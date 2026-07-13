using Microsoft.Extensions.Logging;
using ReplicaGuard.Application.Assets.Services;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Capabilities;
using ReplicaGuard.Domain.HosterAccounts;
using ReplicaGuard.Domain.Hosters;
using ReplicaGuard.Domain.Replication;

namespace ReplicaGuard.Application.Replication.Recovery;

public interface IReplicaRecoveryService
{
    Task Recover(Asset asset, Replica replica, CancellationToken ct);
}

internal sealed class ReplicaRecoveryService : IReplicaRecoveryService
{
    private readonly IHosterAccountRepository _accountRepo;
    private readonly IHosterRepository _hosterRepo;
    private readonly IHosterDefinitionResolver _hosterDefinitions;
    private readonly IReplicaExpiryPredictionService _expiryPrediction;
    private readonly ICapabilityFactory _capabilityFactory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReplicaRecoveryService> _logger;

    public ReplicaRecoveryService(
        IHosterAccountRepository accountRepo,
        IHosterRepository hosterRepo,
        IHosterDefinitionResolver hosterDefinitions,
        IReplicaExpiryPredictionService expiryPrediction,
        ICapabilityFactory capabilityFactory,
        IUnitOfWork unitOfWork,
        ILogger<ReplicaRecoveryService> logger)
    {
        _accountRepo = accountRepo;
        _hosterRepo = hosterRepo;
        _hosterDefinitions = hosterDefinitions;
        _expiryPrediction = expiryPrediction;
        _capabilityFactory = capabilityFactory;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Recover(Asset asset, Replica replica, CancellationToken ct)
    {
        // First check if file is still laive never trust the stuas if not makr as dead nd return
        if (replica.AvailabilityStatus == ReplicaAvailabilityStatus.Expired)
            return;

        var originalId = replica.SourceReplicaId ?? replica.Id;

        var hoster = await _hosterRepo.GetByIdAsync(replica.HosterId, ct);
        if (hoster == null)
            return;

        var definition = _hosterDefinitions.Resolve(hoster.Code);

        // CopyFile requires an account — anonymous replicas can't be recovered this way
        if (!replica.HosterAccountId.HasValue)
        {
            _logger.LogWarning(
                "Replica {ReplicaId} has no account — cannot copy. Marking as Tombstoned.",
                replica.Id);
            replica.MarkAsTombstoned();
            return;
        }

        // Check if hoster supports CopyFile
        var copyRequirement = definition.GetRequirement(CapabilityCode.CopyFile);
        if (copyRequirement is null)
        {
            _logger.LogWarning(
                "Hoster {HosterCode} does not support CopyFile — cannot recover replica {ReplicaId}. Marking as Tombstoned.",
                hoster.Code, replica.Id);
            replica.MarkAsTombstoned();
            return;
        }

        // Load account with secrets
        var account = await _accountRepo.GetByIdAsync(replica.HosterAccountId.Value, ct);
        if (account is null)
        {
            _logger.LogWarning(
                "Account {AccountId} not found for replica {ReplicaId}. Marking as Tombstoned.",
                replica.HosterAccountId.Value, replica.Id);
            replica.MarkAsTombstoned();
            return;
        }

        // Validate account can perform CopyFile
        var validation = definition.ValidateCapability(account, CapabilityCode.CopyFile);
        if (validation.IsFailure)
        {
            _logger.LogWarning(
                "Account validation failed for CopyFile on replica {ReplicaId}: {Error}. Marking as Tombstoned.",
                replica.Id, validation.Error);
            replica.MarkAsTombstoned();
            return;
        }

        // Attempt copy
        var copyHandler = _capabilityFactory.Get<ICopyFileCapabilityHandler>(hoster.Code);
        var copyResult = await copyHandler.HandleAsync(
            new CopyFileRequest(account, replica.Link!), ct);

        if (copyResult.IsFailure)
        {
            _logger.LogWarning(
                "CopyFile failed for replica {ReplicaId}: {Error}. Marking as Tombstoned.",
                replica.Id, copyResult.Error);
            replica.MarkAsTombstoned();
            return;
        }

        // Build new URL from file code
        var urlResult = definition.BuildFileUrl(copyResult.Value.FileCode);
        if (urlResult.IsFailure)
        {
            _logger.LogWarning(
                "BuildFileUrl failed for replica {ReplicaId}: {Error}",
                replica.Id, urlResult.Error);
            replica.MarkAsTombstoned();
            return;
        }

        // Create backup replica
        var replicaAddResult = asset.AddReplicaBackup(
            asset.Id,
            replica.HosterId,
            replica.HosterAccountId,
            urlResult.Value,
            DateTime.UtcNow,
            originalId);

        if (replicaAddResult.IsFailure)
        {
            _logger.LogWarning(
                "Failed to create backup replica for {OriginalId} on {HosterCode}",
                originalId, hoster.Code);
            return;
        }

        var newReplica = replicaAddResult.Value;

        // Predict expiry for the new backup
        //var expiryResult = await _expiryPrediction.Predict(definition, newReplica);
        //if (expiryResult.IsSuccess)
        //    newReplica.SetPredictedExpiry(expiryResult.Value, DateTime.UtcNow);

        //_repo.Add(newReplica);

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Created backup replica {NewReplicaId} (from {OriginalId}) on {HosterCode}",
            newReplica.Id, originalId, hoster.Code);
    }
}
