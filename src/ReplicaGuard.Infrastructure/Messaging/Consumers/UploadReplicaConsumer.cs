using MassTransit;
using Microsoft.Extensions.Logging;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Capabilities;
using ReplicaGuard.Core.Capabilities.Credentials;
using ReplicaGuard.Core.Capabilities.Upload;
using ReplicaGuard.Core.Domain.Credentials;
using ReplicaGuard.Core.Domain.Hoster;
using ReplicaGuard.Core.Domain.Replication;
using ReplicaGuard.Core.Domain.Replication.Planner;
using ReplicaGuard.Infrastructure.Hosters;
using ReplicaGuard.Infrastructure.Messaging.Commands;
using ReplicaGuard.Infrastructure.Spool;
using RetryPolicy = ReplicaGuard.Core.Domain.Replication.Policies.IRetryPolicy;

namespace ReplicaGuard.Infrastructure.Messaging.Consumers;

public sealed class UploadReplicaConsumer : IConsumer<UploadReplicaCommand>
{
    private readonly IUploadPlanner _planner;
    private readonly ISpoolLeaseService _leases;
    private readonly IAssetRepository _assets;
    private readonly IHosterRepository _hosters;
    private readonly IHosterCredentialsRepository _credentials;
    private readonly IHosterClientRegistry _hosterRegistry;
    private readonly FileFetcher _fileFetcher;
    private readonly RetryPolicy _retryPolicy;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UploadReplicaConsumer> _logger;

    public UploadReplicaConsumer(
        IUploadPlanner planner,
        ISpoolLeaseService leases,
        IAssetRepository assets,
        IHosterRepository hosters,
        IHosterCredentialsRepository credentials,
        IHosterClientRegistry hosterRegistry,
        FileFetcher fileFetcher,
        RetryPolicy retryPolicy,
        IUnitOfWork unitOfWork,
        ILogger<UploadReplicaConsumer> logger)
    {
        _planner = planner;
        _leases = leases;
        _assets = assets;
        _hosters = hosters;
        _credentials = credentials;
        _hosterRegistry = hosterRegistry;
        _fileFetcher = fileFetcher;
        _retryPolicy = retryPolicy;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UploadReplicaCommand> context)
    {
        var ct = context.CancellationToken;
        var cmd = context.Message;

        var asset = await _assets.GetByIdWithReplicasAsync(cmd.AssetId, ct);
        if (asset == null)
        {
            _logger.LogWarning("Asset {AssetId} not found", cmd.AssetId);
            return;
        }

        var replica = asset.Replicas.FirstOrDefault(r => r.Id == cmd.ReplicaId);
        if (replica == null)
        {
            _logger.LogWarning("Replica {ReplicaId} not found", cmd.ReplicaId);
            return;
        }

        if (replica.IsTerminal)
            return;

        var hoster = await _hosters.GetByIdAsync(cmd.HosterId, ct);
        if (hoster == null)
        {
            _logger.LogWarning("Hoster {HosterId} not found", cmd.HosterId);
            return;
        }

        var uploader = _hosterRegistry.TryGetHosterCapability<IUploadFile>(hoster.Code);
        if (uploader == null)
        {
            var failure = asset.RecordFailure(replica, PermanentErrors.UploadNotSupported);
            await _unitOfWork.SaveChangesAsync(ct);
            return;
        }

        var credentials = await _credentials.FindByUserAndHosterAsync(asset.UserId, hoster.Id, ct);
        if (credentials == null)
        {
            var failure = asset.RecordFailure(replica, PermanentErrors.NoCredentials);
            await _unitOfWork.SaveChangesAsync(ct);
            return;
        }

        var creds = new CredentialSet(credentials.ApiKey, credentials.Email, credentials.Username, credentials.Password);

        var lease = await _leases.GetAsync(asset.Id, ct);
        var spool = SpoolStateFactory.Create(asset.Id, _fileFetcher, lease);

        var def = HosterDefinitions.All.FirstOrDefault(x => x.Code == hoster.Code);
        if (def == null)
        {
            asset.Fail(replica, "HosterDefinition.Missing");
            await _unitOfWork.SaveChangesAsync(ct);
            return;
        }

        var caps = new HosterCapabilities(
            Code: hoster.Code,
            SupportsSpooledUpload: def.Features.Any(f => f.Feature == CapabilityCode.SpooledUpload),
            SupportsRemoteFetch: def.Features.Any(f => f.Feature == CapabilityCode.RemoteUpload)
        );

        var decision = _planner.Plan(asset, cmd.ReplicaId, caps, spool);

        try
        {
            await ApplyDecision(asset, replica, cmd, decision, uploader, creds, context, ct);
        }
        catch (ConcurrencyException)
        {
            await _leases.ReleaseAsync(asset.Id, replica.Id, ct);
            var delay = _retryPolicy.GetDelay(replica.RetryCount);
            await context.SchedulePublish(delay, cmd, ct);
        }
    }

    private async Task ApplyDecision(
        Asset asset,
        Replica replica,
        UploadReplicaCommand cmd,
        UploadDecision decision,
        IUploadFile uploader,
        CredentialSet creds,
        ConsumeContext context,
        CancellationToken ct)
    {
        if (replica.IsTerminal)
            return;

        switch (decision)
        {
            case UploadDecision.NoOp:
                _logger.LogDebug("NoOp for Replica {ReplicaId}", replica.Id);
                return;

            case UploadDecision.FailPermanent f:
                asset.Fail(replica, f.Reason);
                await _unitOfWork.SaveChangesAsync(ct);
                return;

            case UploadDecision.WaitForDownload:
                await context.SchedulePublish(TimeSpan.FromSeconds(5), cmd, ct);
                return;

            case UploadDecision.WaitForPeer w:
                asset.MarkWaitingForPeer(replica, w.PeerId);
                await _unitOfWork.SaveChangesAsync(ct);
                await context.SchedulePublish(w.Timeout, cmd, ct);
                return;

            case UploadDecision.DownloadToSpool d:
                {
                    // TODO: when downloading update ttl
                    var acquired = await _leases.TryAcquireAsync(asset.Id, cmd.ReplicaId, TimeSpan.FromMinutes(30), ct); // TODO: make it configurable
                    if (acquired is null)
                    {
                        var delay = _retryPolicy.GetDelay(replica.RetryCount); // TODO: Use remaining ttl instead of retry count based delay
                        await context.SchedulePublish(delay, cmd, ct);
                        return;
                    }


                    asset.StartDownloading(replica);
                    await _unitOfWork.SaveChangesAsync(ct);

                    var fetched = await _fileFetcher.DownloadAsync(asset.Id, d.Source, ct);

                    if (fetched.IsFailure)
                    {
                        var failure = asset.RecordFailure(replica, fetched.Error);
                        await _unitOfWork.SaveChangesAsync(ct);

                        if (failure.IsSuccess && failure.Value == FailureDecision.Retryable)
                        {
                            var delay = _retryPolicy.GetDelay(replica.RetryCount);
                            await context.SchedulePublish(delay, cmd, ct);
                        }
                        return;
                    }

                    asset.RecordFileSize(fetched.Value.SizeBytes);
                    await _unitOfWork.SaveChangesAsync(ct);
                    await _leases.ReleaseAsync(asset.Id, cmd.ReplicaId, ct);

                    await context.SchedulePublish(TimeSpan.FromSeconds(1), cmd, ct);
                    return;
                }

            case UploadDecision.UploadFromRemote r:
                {
                    asset.StartDownloading(replica);
                    await _unitOfWork.SaveChangesAsync(ct);

                    var result = await uploader.UploadFromRemoteUrlAsync(creds, asset.FileName, r.Source, ct);
                    await ApplyUploadResult(asset, replica, cmd, result, context, ct);
                    return;
                }

            case UploadDecision.UploadFromLocal l:
                {
                    if (!File.Exists(l.FilePath))
                    {
                        var failure = asset.RecordFailure(replica, new Error("File.NotFound", "Local file missing"));
                        await _unitOfWork.SaveChangesAsync(ct);
                        return;
                    }

                    asset.StartUploading(replica);
                    await _unitOfWork.SaveChangesAsync(ct);
                    await using var stream = File.OpenRead(l.FilePath);
                    var result = await uploader.UploadFromLocalStorageAsync(creds, asset.FileName, stream, ct);

                    await ApplyUploadResult(asset, replica, cmd, result, context, ct);
                    return;
                }

            default:
                throw new InvalidOperationException($"Unknown decision {decision}");
        }
    }

    private async Task ApplyUploadResult(
        Asset asset,
        Replica replica,
        UploadReplicaCommand cmd,
        Result<UploadResponse> result,
        ConsumeContext context,
        CancellationToken ct)
    {
        while (true)
        {
            try
            {
                // Reload the asset to get the latest version and replica state
                var refreshedAsset = await _assets.GetByIdWithReplicasAsync(asset.Id, ct);
                if (refreshedAsset == null)
                {
                    _logger.LogWarning("Asset {AssetId} not found during upload result processing", cmd.AssetId);
                    return;
                }

                var refreshedReplica = refreshedAsset.Replicas.FirstOrDefault(r => r.Id == cmd.ReplicaId);
                if (refreshedReplica == null)
                {
                    _logger.LogWarning("Replica {ReplicaId} not found during upload result processing", cmd.ReplicaId);
                    return;
                }

                if (refreshedReplica.IsTerminal)
                    return;

                if (result.IsSuccess)
                {
                    var response = result.Value;

                    if (response.SizeBytes is not null)
                        refreshedAsset.RecordFileSize(response.SizeBytes.Value);

                    refreshedAsset.Complete(refreshedReplica, response.FileUrl);
                    await _unitOfWork.SaveChangesAsync(ct);
                    return;
                }

                var failure = refreshedAsset.RecordFailure(refreshedReplica, result.Error);
                await _unitOfWork.SaveChangesAsync(ct);

                if (failure.IsSuccess && failure.Value == FailureDecision.Retryable)
                {
                    var delay = _retryPolicy.GetDelay(refreshedReplica.RetryCount);
                    await context.SchedulePublish(delay, cmd, ct);
                }
                return;
            }
            catch (ConcurrencyException ex)
            {
                _logger.LogDebug(
                    ex,
                    "Concurrency conflict applying upload result for Replica {ReplicaId}. Retrying...",
                    cmd.ReplicaId);

                // Retry without limit - keep trying until successful
                await Task.Delay(100, ct);
            }
        }
    }
}
