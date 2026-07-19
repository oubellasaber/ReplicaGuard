using Microsoft.Extensions.Logging;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Capabilities;
using ReplicaGuard.Domain.HosterAccounts;
using ReplicaGuard.Domain.Hosters;
using ReplicaGuard.Domain.Replication;

namespace ReplicaGuard.Infrastructure.Recovery;

public interface IReplicaRecoveryService
{
    Task Recover(Asset asset, Replica replica, CancellationToken ct);
}

internal sealed class ReplicaRecoveryService : IReplicaRecoveryService
{
    private readonly IHosterAccountRepository _accountRepo;
    private readonly IHosterRepository _hosterRepo;
    private readonly IHosterDefinitionResolver _hosterDefinitions;
    private readonly ICapabilityFactory _capabilityFactory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReplicaRecoveryService> _logger;

    public ReplicaRecoveryService(
        IHosterAccountRepository accountRepo,
        IHosterRepository hosterRepo,
        IHosterDefinitionResolver hosterDefinitions,
        ICapabilityFactory capabilityFactory,
        IUnitOfWork unitOfWork,
        ILogger<ReplicaRecoveryService> logger)
    {
        _accountRepo = accountRepo;
        _hosterRepo = hosterRepo;
        _hosterDefinitions = hosterDefinitions;
        _capabilityFactory = capabilityFactory;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Recover(Asset asset, Replica replica, CancellationToken ct)
    {
        var originalId = replica.SourceReplicaId ?? replica.Id;

        var hoster = await _hosterRepo.GetByIdAsync(replica.HosterId, ct);
        if (hoster == null) return;

        var definition = _hosterDefinitions.Resolve(hoster.Code);
        bool recoveryFailed = false;

        // Can't recover without a Link
        if (replica.Link is null)
        {
            _logger.LogWarning("Replica {ReplicaId} has no Link — cannot copy.", replica.Id);
            recoveryFailed = true;
        }

        // CopyFile requires an account
        if (!replica.HosterAccountId.HasValue)
        {
            _logger.LogWarning("Replica {ReplicaId} has no account — cannot copy.", replica.Id);
            recoveryFailed = true;
        }

        // Check if hoster supports CopyFile
        var copyRequirement = definition.GetRequirement(CapabilityCode.CopyFile);
        if (copyRequirement is null)
        {
            _logger.LogWarning("Hoster {Code} does not support CopyFile — cannot recover replica {Id}.", hoster.Code, replica.Id);
            recoveryFailed = true;
        }

        HosterAccount? account = null;
        if (!recoveryFailed)
        {
            account = await _accountRepo.GetByIdAsync(replica.HosterAccountId!.Value, ct);
            if (account is null)
            {
                _logger.LogWarning("Account {AccountId} not found for replica {ReplicaId}.", replica.HosterAccountId.Value, replica.Id);
                recoveryFailed = true;
            }
        }

        if (!recoveryFailed)
        {
            var validation = definition.ValidateCapability(account!, CapabilityCode.CopyFile);
            if (validation.IsFailure)
            {
                _logger.LogWarning("Account validation failed for CopyFile on replica {Id}: {Error}.", replica.Id, validation.Error);
                recoveryFailed = true;
            }
        }

        if (!recoveryFailed)
        {
            var copyHandler = _capabilityFactory.Get<ICopyFileCapabilityHandler>(hoster.Code);
            var copyResult = await copyHandler.HandleAsync(new CopyFileRequest(account!, replica.Link!), ct);

            if (copyResult.IsFailure)
            {
                _logger.LogWarning("CopyFile failed for replica {Id}: {Error}.", replica.Id, copyResult.Error);
                recoveryFailed = true;
            }
            else
            {
                var urlResult = definition.BuildFileUrl(copyResult.Value.FileCode);
                if (urlResult.IsFailure)
                {
                    _logger.LogWarning("BuildFileUrl failed for replica {Id}: {Error}", replica.Id, urlResult.Error);
                    recoveryFailed = true;
                }
                else
                {
                    var addResult = asset.AddReplicaBackup(
                        replica.HosterId,
                        replica.HosterAccountId,
                        urlResult.Value,
                        DateTime.UtcNow,
                        originalId);

                    if (addResult.IsFailure)
                    {
                        _logger.LogWarning("Failed to create backup replica for {OriginalId} on {Code}", originalId, hoster.Code);
                        recoveryFailed = true;
                    }
                    else
                    {
                        replica.MarkAsTombstoned();
                        _logger.LogInformation("Created backup replica {NewId} (from {OriginalId}) on {Code}", addResult.Value.Id, originalId, hoster.Code);
                    }
                }
            }
        }

        if (recoveryFailed)
        {
            replica.RecordRecoveryAttempt();
            _logger.LogWarning("Recovery attempt {N} failed for replica {Id}", replica.RecoveryAttemptCount, replica.Id);
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }
}
