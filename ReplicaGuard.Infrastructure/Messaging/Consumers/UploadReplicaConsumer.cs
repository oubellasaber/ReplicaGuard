using MassTransit;
using Microsoft.Extensions.Logging;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Capabilities;
using ReplicaGuard.Core.Capabilities.Credentials;
using ReplicaGuard.Core.Capabilities.Upload;
using ReplicaGuard.Core.Domain.Credentials;
using ReplicaGuard.Core.Domain.Hoster;
using ReplicaGuard.Core.Domain.Replication;
using ReplicaGuard.Infrastructure.Hosters;
using ReplicaGuard.Infrastructure.Messaging.Commands;

namespace ReplicaGuard.Infrastructure.Messaging.Consumers;

public sealed class UploadReplicaConsumer : IConsumer<UploadReplicaCommand>
{
    private readonly IAssetRepository _assetRepository;
    private readonly IReplicaRepository _replicaRepository;
    private readonly IHosterRepository _hosterRepository;
    private readonly IHosterCredentialsRepository _credentialsRepository;
    private readonly IHosterClientRegistry _hosterRegistry;
    private readonly FileFetcher _fileFetcher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UploadReplicaConsumer> _logger;

    public UploadReplicaConsumer(
        IAssetRepository assetRepository,
        IReplicaRepository replicaRepository,
        IHosterRepository hosterRepository,
        IHosterCredentialsRepository credentialsRepository,
        IHosterClientRegistry hosterRegistry,
        FileFetcher fileFetcher,
        IUnitOfWork unitOfWork,
        ILogger<UploadReplicaConsumer> logger)
    {
        _assetRepository = assetRepository;
        _replicaRepository = replicaRepository;
        _hosterRepository = hosterRepository;
        _credentialsRepository = credentialsRepository;
        _hosterRegistry = hosterRegistry;
        _fileFetcher = fileFetcher;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UploadReplicaCommand> context)
    {
        CancellationToken ct = context.CancellationToken;
        UploadReplicaCommand message = context.Message;

        Replica? replica = await _replicaRepository.GetByIdAsync(message.ReplicaId, ct);
        if (replica == null)
        {
            _logger.LogWarning(
                "Replica {ReplicaId} not found; skipping upload command",
                message.ReplicaId); 
            return;
        }

        if (replica.State is ReplicaState.Completed or ReplicaState.Uploading)
        {
            _logger.LogInformation(
                "Replica {ReplicaId} already in terminal state {ReplicaState}.",
                replica.Id,
                replica.State);
            return;
        }

        if (replica.State == ReplicaState.Failed && !replica.CanRetry())
        {
            _logger.LogWarning(
                "Replica {ReplicaId} exceeded retry attempts; dropping command",
                replica.Id);
            return;
        }

        Asset? asset = await _assetRepository.GetByIdWithReplicasAsync(message.AssetId, ct);
        if (asset == null)
        {
            _logger.LogWarning(
                "Asset {AssetId} not found for Replica {ReplicaId}; skipping",
                message.AssetId,
                replica.Id);
            return;
        }

        Hoster? hoster = await _hosterRepository.GetByIdAsync(message.HosterId, ct);
        if (hoster == null)
        {
            _logger.LogWarning(
                "Hoster {HosterId} not found for Replica {ReplicaId}; skipping",
                message.HosterId,
                replica.Id);
            return;
        }

        IUploadFile? uploader = _hosterRegistry.TryGetHosterCapability<IUploadFile>(hoster.Code);
        if (uploader == null)
        {
            _logger.LogWarning(
                "Hoster {HosterCode} does not expose an upload capability; failing Replica {ReplicaId}",
                hoster.Code,
                replica.Id);
            FailReplica(replica, asset, $"Hoster '{hoster.Code}' does not support uploads");
            await _unitOfWork.SaveChangesAsync(ct);
            return;
        }

        HosterCredentials? creds = await _credentialsRepository.FindByUserAndHosterAsync(
            asset.UserId, hoster.Id, ct);

        if (creds == null)
        {
            _logger.LogWarning(
                "Missing credentials for hoster {HosterCode} and user {UserId}; failing Replica {ReplicaId}",
                hoster.Code,
                asset.UserId,
                replica.Id);
            FailReplica(replica, asset, $"No credentials for hoster '{hoster.Code}'");
            await _unitOfWork.SaveChangesAsync(ct);
            return;
        }

        CredentialSet credentialSet = new(creds.ApiKey, creds.Email, creds.Username, creds.Password);

        try
        {
            Result<UploadResponse>? result = await ResolveAndUploadAsync(
                uploader, credentialSet, asset, replica, hoster.Code, context, ct);

            // null means replica is now waiting for a peer
            if (result == null)
            {
                _logger.LogInformation(
                    "Replica {ReplicaId} is waiting for a sibling replica before uploading",
                    replica.Id);
                return;
            }

            if (result.IsFailure)
            {
                _logger.LogWarning(
                    "Replica {ReplicaId} upload failed: {ErrorCode} - {ErrorMessage}",
                    replica.Id,
                    result.Error.Code,
                    result.Error.Message);

                FailReplica(replica, asset, result.Error.Message);
                await _unitOfWork.SaveChangesAsync(ct);

                if (replica.CanRetry())
                {
                    TimeSpan delay = CalculateRetryDelay(replica.RetryCount);
                    await context.SchedulePublish(delay, message);

                    _logger.LogInformation(
                        "Scheduled retry {RetryAttempt} for Replica {ReplicaId} in {Delay}",
                        replica.RetryCount,
                        replica.Id,
                        delay);
                }

                return;
            }

            replica.MarkCompleted(result.Value.FileUrl);
            asset.RecalculateState();
            await _unitOfWork.SaveChangesAsync(ct);

            _logger.LogInformation("Replica {ReplicaId} completed: {Url}",
                replica.Id, result.Value.FileUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error for Replica {ReplicaId}", replica.Id);
            FailReplica(replica, asset, ex.Message);
            await _unitOfWork.SaveChangesAsync(ct);
            throw;
        }
    }

    /// <summary>
    /// Determines the best upload path and executes it.
    /// Returns null if the replica is now waiting for a peer.
    /// </summary>
    private async Task<Result<UploadResponse>?> ResolveAndUploadAsync(
        IUploadFile uploader,
        CredentialSet credentials,
        Asset asset,
        Replica replica,
        string hosterCode,
        ConsumeContext context,
        CancellationToken ct)
    {
        // 1. Remote source => try letting hoster fetch from URL directly
        if (asset.Source is RemoteFileSource remoteSource)
        {
            Result<UploadResponse> remoteResult = await uploader.UploadFromRemoteUrlAsync(
                credentials, asset.FileName.Value, remoteSource, ct);

            if (remoteResult.IsSuccess)
            {
                _logger.LogInformation("{Hoster} fetched from remote URL directly", hosterCode);

                Result markUploadingResult = replica.MarkUploading();
                if (markUploadingResult.IsFailure)
                    return Result.Failure<UploadResponse>(markUploadingResult.Error);

                if (remoteResult.Value.SizeBytes.HasValue)
                    asset.RecordFileSize(remoteResult.Value.SizeBytes.Value);

                asset.RecalculateState();
                await _unitOfWork.SaveChangesAsync(ct);

                return remoteResult;
            }

            // Real failure (not just "unsupported") => return it
            if (!remoteResult.Error.Code.Contains("MethodNotSupported"))
                return remoteResult;

            // Hoster doesn't support remote URL => need local file
            _logger.LogInformation("{Hoster} doesn't support remote URL, need local file", hosterCode);

            // Is a sibling already downloading? Wait for it to spool the file
            Replica? busySibling = asset.Replicas.FirstOrDefault(r =>
                r.Id != replica.Id && r.State == ReplicaState.Downloading);

            if (busySibling != null && !_fileFetcher.IsSpooled(asset.Id))
            {
                _logger.LogInformation(
                    "Replica {ReplicaId} waiting for sibling {SiblingId} to finish downloading",
                    replica.Id, busySibling.Id);

                Result waitingResult = replica.MarkWaitingForPeer(busySibling.Id);
                if (waitingResult.IsFailure)
                    return Result.Failure<UploadResponse>(waitingResult.Error);

                asset.RecalculateState();
                await _unitOfWork.SaveChangesAsync(ct);

                // Safety timeout in case sibling fails silently
                await context.SchedulePublish(
                    TimeSpan.FromMinutes(10),
                    new UploadReplicaCommand(replica.Id, asset.Id, replica.HosterId));

                return null;
            }

            // Nobody downloading or file already spooled => download ourselves
            Result markDownloadingResult = replica.MarkDownloading();
            if (markDownloadingResult.IsFailure)
                return Result.Failure<UploadResponse>(markDownloadingResult.Error);

            asset.RecalculateState();
            await _unitOfWork.SaveChangesAsync(ct);

            Result<FetchedFile> fetchResult = await _fileFetcher.DownloadAsync(asset.Id, remoteSource, ct);
            if (fetchResult.IsFailure)
                return Result.Failure<UploadResponse>(fetchResult.Error);

            // We measured the file — record the size
            asset.RecordFileSize(fetchResult.Value.SizeBytes);
        }

        // 2. Upload from local file
        string filePath = asset.Source switch
        {
            LocalFileSource local => local.FilePath,
            RemoteFileSource => _fileFetcher.GetSpoolPath(asset.Id),
            _ => throw new InvalidOperationException($"Unknown source: {asset.Source?.GetType()}")
        };

        if (!File.Exists(filePath))
            return Result.Failure<UploadResponse>(ReplicationErrors.FileNotFound(filePath));

        // For local files, record size from disk
        asset.RecordFileSize(new FileInfo(filePath).Length);

        replica.MarkUploading();
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("{Hoster} uploading from {Path}", hosterCode, filePath);

        await using FileStream stream = File.OpenRead(filePath);
        return await uploader.UploadFromLocalStorageAsync(
            credentials, asset.FileName.Value, stream, ct);
    }

    private static void FailReplica(Replica replica, Asset asset, string reason)
    {
        replica.MarkFailed(reason);
        asset.RecalculateState();
    }

    private static TimeSpan CalculateRetryDelay(int retryCount)
    {
        const double initialSeconds = 30d;
        const double maxSeconds = 600d;

        int attempt = Math.Max(1, retryCount);
        double exponential = Math.Pow(2, attempt - 1);
        double seconds = Math.Min(initialSeconds * exponential, maxSeconds);
        double jitterFactor = 0.8 + (Random.Shared.NextDouble() * 0.4); // +/-20% jitter

        return TimeSpan.FromSeconds(seconds * jitterFactor);
    }
}
