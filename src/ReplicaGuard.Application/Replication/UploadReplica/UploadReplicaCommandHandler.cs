using MediatR;
using Microsoft.Extensions.Logging;
using ReplicaGuard.Application.Abstractions.Clock;
using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Application.Replication.UploadReplica.Fetching;
using ReplicaGuard.Application.Replication.UploadReplica.Spooling;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Capabilities;
using ReplicaGuard.Core.Capabilities.Credentials;
using ReplicaGuard.Core.Capabilities.Upload;
using ReplicaGuard.Core.Domain.Credentials;
using ReplicaGuard.Core.Domain.Hoster;
using ReplicaGuard.Core.Domain.Replication;

namespace ReplicaGuard.Application.Replication.UploadReplica;

public sealed class UploadReplicaCommandHandler : ICommandHandler<UploadReplicaCommand, Unit>
{
    private readonly IReplicaRepository _replicaRepository;
    private readonly IHosterRepository _hosters;
    private readonly IHosterCredentialsRepository _credentials;
    private readonly IAssetRepository _assets;
    private readonly IHosterClientRegistry _hosterRegistry;
    private readonly ISpoolLeaseService _leases;
    private readonly IFileFetcher _fileFetcher;
    private readonly ISpoolFileLocator _fileLocator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<UploadReplicaCommandHandler> _logger;

    public UploadReplicaCommandHandler(
        IReplicaRepository replicaRepository,
        IHosterRepository hosters,
        IHosterCredentialsRepository credentials,
        IAssetRepository assets,
        IHosterClientRegistry hosterRegistry,
        ISpoolLeaseService leases,
        IFileFetcher fileFetcher,
        ISpoolFileLocator fileLocator,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        ILogger<UploadReplicaCommandHandler> logger)
    {
        _replicaRepository = replicaRepository;
        _hosters = hosters;
        _credentials = credentials;
        _assets = assets;
        _hosterRegistry = hosterRegistry;
        _leases = leases;
        _fileFetcher = fileFetcher;
        _fileLocator = fileLocator;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<Unit>> Handle(UploadReplicaCommand cmd, CancellationToken ct)
    {
        // 1. Load + validate everything
        var context = await LoadAndValidateAsync(cmd, ct);
        if (context.IsFailure) return Result.Failure<Unit>(context.Error);

        // 2. Route by source type
        try
        {
            return context.Value.Asset.Source switch
            {
                LocalFileSource local => await HandleLocalAsync(context.Value, local, ct),
                RemoteFileSource remote => await HandleRemoteAsync(context.Value, remote, ct),
                _ => throw new InvalidOperationException("Unknown source type")
            };
        }
        catch
        {
            if (cmd.IsLastRetry)
                context.Value.Replica.MarkAsFailed(_dateTimeProvider.UtcNow);
            else
                context.Value.Replica.MarkAsRetrying(_dateTimeProvider.UtcNow);
            await _unitOfWork.SaveChangesAsync(ct);
            throw;
        }
        finally
        {
            await _leases.ReleaseForAsset(cmd.AssetId);
        }
    }

    private async Task<Result<UploadContext>> LoadAndValidateAsync(
        UploadReplicaCommand cmd,
        CancellationToken ct)
    {
        // 1) Validation
        var replica = await _replicaRepository.GetByIdAsync(cmd.ReplicaId, ct);
        if (replica is null)
            return Result.Failure<UploadContext>(ReplicationErrors.ReplicaNotFound(cmd.ReplicaId).AsPermanent());

        if (replica.IsTerminal)
            return Result.Success(new UploadContext(cmd, replica)); // short-circuit

        var asset = await _assets.GetByIdWithReplicasAsync(cmd.AssetId, ct);
        if (asset == null)
        {
            _logger.LogWarning("Asset {AssetId} not found", cmd.AssetId);
            return Result.Failure<UploadContext>(ReplicationErrors.AssetNotFound(cmd.AssetId).AsPermanent());
        }

        var hoster = await _hosters.GetByIdAsync(cmd.HosterId, ct);
        if (hoster == null)
        {
            _logger.LogWarning("Hoster {HosterId} not found", cmd.HosterId);
            return Result.Failure<UploadContext>(HosterErrors.NotFound(cmd.HosterId).AsPermanent());
        }

        var credentials = await _credentials.FindByUserAndHosterAsync(asset.UserId, hoster.Id, ct);
        if (credentials == null)
        {
            replica.MarkAsFailed(_dateTimeProvider.UtcNow);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result.Failure<UploadContext>(UploadReplicaErrors.NoCredentials(hoster.Code));
        }

        var uploader = _hosterRegistry.TryGetHosterCapability<IUploadFile>(hoster.Code);
        if (uploader == null)
        {
            replica.MarkAsFailed(_dateTimeProvider.UtcNow);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result.Failure<UploadContext>(UploadReplicaErrors.UploadNotSupported(hoster.Code));
        }

        var creds = new CredentialSet(credentials.ApiKey, credentials.Email, credentials.Username, credentials.Password);
        var def = HosterDefinitions.All.FirstOrDefault(x => x.Code == hoster.Code);
        if (def == null)
        {
            replica.MarkAsFailed(_dateTimeProvider.UtcNow);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result.Failure<UploadContext>(HosterErrors.NotFound(hoster.Id).AsPermanent());
        }

        var lease = await _leases.GetAsync(asset.Id, ct);
        var spool = SpoolStateResolver.Resolve(cmd.AssetId, _fileLocator, lease);

        var caps = new HosterCapabilities(
            Code: hoster.Code,
            SupportsSpooledUpload: def.Features.Any(f => f.Feature == CapabilityCode.SpooledUpload),
            SupportsRemoteFetch: def.Features.Any(f => f.Feature == CapabilityCode.RemoteUpload)
        );

        return Result.Success(new UploadContext(
            cmd,
            asset,
            replica,
            hoster,
            uploader,
            creds,
            caps,
            spool
        ));
    }

    private async Task<Result<Unit>> HandleLocalAsync(
        UploadContext ctx,
        LocalFileSource local,
        CancellationToken ct)
    {
        if (!File.Exists(local.FilePath))
        {
            _logger.LogWarning("Local file {FilePath} not found for asset {AssetId}", local.FilePath, ctx.Asset.Id);
            ctx.Replica.MarkAsFailed(_dateTimeProvider.UtcNow);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result.Failure<Unit>(UploadReplicaErrors.LocalFileNotFound(local.FilePath));
        }

        ctx.Replica.MarkAsUploading(_dateTimeProvider.UtcNow);
        await _unitOfWork.SaveChangesAsync(ct);

        await using var stream = File.OpenRead(local.FilePath);
        var result = await ctx.Uploader.UploadFromLocalStorageAsync(ctx.Creds, ctx.Asset.FileName, stream, ct);

        if (result.IsFailure)
        {
            ctx.Replica.MarkAsFailed(_dateTimeProvider.UtcNow);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result.Failure<Unit>(result.Error);
        }

        ctx.Replica.MarkAsCompleted(result.Value.FileUrl, _dateTimeProvider.UtcNow);
        if (result.Value.SizeBytes is long bytes)
            ctx.Asset.RecordFileSize(bytes, _dateTimeProvider.UtcNow);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(Unit.Value);
    }

    private async Task<Result<Unit>> HandleRemoteAsync(UploadContext ctx, RemoteFileSource source, CancellationToken ct)
    {
        if (ctx.Caps.SupportsRemoteFetch)
            return await UploadFromRemoteUrlAsync(ctx, source, ct);

        return ctx.Spool.Status switch
        {
            SpoolStatus.NotExist => await SpoolAndDownloadAsync(ctx, source, ct),
            SpoolStatus.Downloading => WaitForSpool(ctx),   // return success, gonna waked up probably
            SpoolStatus.Completed => await UploadFromSpoolAsync(ctx, ct),
            _ => throw new InvalidOperationException()
        };
    }

    private async Task<Result<Unit>> UploadFromRemoteUrlAsync(
        UploadContext ctx,
        RemoteFileSource remoteSource,
        CancellationToken ct)
    {
        ctx.Replica.MarkAsUploading(_dateTimeProvider.UtcNow);
        await _unitOfWork.SaveChangesAsync(ct);

        var result = await ctx.Uploader.UploadFromRemoteUrlAsync(ctx.Creds, ctx.Asset.FileName, remoteSource, ct);
        if (result.IsFailure)
        {
            if (ctx.Cmd.IsLastRetry || result.Error.IsPermanent)
                ctx.Replica.MarkAsFailed(_dateTimeProvider.UtcNow);
            else
                ctx.Replica.MarkAsRetrying(_dateTimeProvider.UtcNow);

            await _unitOfWork.SaveChangesAsync(ct);
            return Result.Failure<Unit>(result.Error);
        }

        ctx.Replica.MarkAsCompleted(result.Value.FileUrl, _dateTimeProvider.UtcNow);
        if (result.Value.SizeBytes is long bytes)
            ctx.Asset.RecordFileSize(bytes, _dateTimeProvider.UtcNow);

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success(Unit.Value);
    }

    private async Task<Result<Unit>> SpoolAndDownloadAsync(
        UploadContext ctx,
        RemoteFileSource remoteSource,
        CancellationToken ct)
    {
        // Try to acquire lease (atomic optimistic concurrency inside TryAcquireAsync)
        var acquired = await _leases.TryAcquireAsync(
            ctx.Asset.Id,
            ctx.Replica.Id,
            TimeSpan.FromMinutes(30),
            ct);

        if (acquired is null)
        {
            // Someone else is downloading.
            // We must atomically decide whether to wait or upload immediately.
            var waitingResult = await _replicaRepository.TryMarkWaitingIfDownloaderStillActive(
                ctx.Asset.Id,
                ctx.Replica.Id,
                ct);

            switch (waitingResult)
            {
                case MarkWaitingResult.AlreadyCompleted:
                    // Downloader finished before we marked waiting => upload immediately
                    ctx = ctx with { Spool = SpoolStateResolver.Resolve(ctx.Cmd.AssetId, _fileLocator, null) };
                    return await UploadFromSpoolAsync(ctx, ct);

                // TODO: When download finishes, this gonna UploadRepilcaCommand gonna republished (MarkAsCompleted has a somian event that will trigger this)
                // but what if it fails permantelly -> requeue waiting replicas
                case MarkWaitingResult.MarkedWaiting:
                    return Result.Success(Unit.Value);

                case MarkWaitingResult.NoActiveDownloader:
                    return Result.Failure<Unit>(UploadReplicaErrors.DownloaderDisappeared);

                default:
                    throw new InvalidOperationException("Unknown waiting result");
            }
        }

        // We are the downloader
        ctx.Replica.MarkAsDownloading(_dateTimeProvider.UtcNow);
        await _unitOfWork.SaveChangesAsync(ct);

        // Download file
        var fetched = await _fileFetcher.DownloadAsync(ctx.Asset.Id, remoteSource, ct);

        if (fetched.IsFailure)
        {
            // Release lease and fail
            _leases.Release(acquired);
            if (ctx.Cmd.IsLastRetry || fetched.Error.IsPermanent)
                ctx.Replica.MarkAsFailed(_dateTimeProvider.UtcNow);
            else
                ctx.Replica.MarkAsRetrying(_dateTimeProvider.UtcNow);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result.Failure<Unit>(fetched.Error);
        }

        // Success: release lease, mark downloaded, wake peers
        _leases.Release(acquired);
        ctx.Asset.RecordFileSize(fetched.Value.SizeBytes, _dateTimeProvider.UtcNow);
        ctx.Replica.MarkAsDownloaded(_dateTimeProvider.UtcNow); // Wake up waiting peers
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(Unit.Value);
    }

    private async Task<Result<Unit>> UploadFromSpoolAsync(
        UploadContext ctx,
        CancellationToken ct)
    {
        ctx.Replica.MarkAsUploading(_dateTimeProvider.UtcNow);
        await _unitOfWork.SaveChangesAsync(ct);

        await using var stream = File.OpenRead(ctx.Spool.FilePath);
        var result = await ctx.Uploader.UploadFromLocalStorageAsync(ctx.Creds, ctx.Asset.FileName, stream, ct);

        if (result.IsFailure)
        {
            if (ctx.Cmd.IsLastRetry || result.Error.IsPermanent)
                ctx.Replica.MarkAsFailed(_dateTimeProvider.UtcNow);
            else
                ctx.Replica.MarkAsRetrying(_dateTimeProvider.UtcNow);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result.Failure<Unit>(result.Error);
        }

        ctx.Replica.MarkAsCompleted(result.Value.FileUrl, _dateTimeProvider.UtcNow);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(Unit.Value);
    }

    private Result<Unit> WaitForSpool(UploadContext ctx)
    {
        // return success, re-enqueue
        return Result.Success(Unit.Value);
    }

    public sealed record UploadContext(
        UploadReplicaCommand Cmd,
        Asset Asset,
        Replica Replica,
        Hoster Hoster,
        IUploadFile Uploader,
        CredentialSet Creds,
        HosterCapabilities Caps,
        SpoolState Spool
    )
    {
        public UploadContext(UploadReplicaCommand cmd, Replica replica)
            : this(cmd, default!, replica, default!, default!, default!, default!, default!) { }
    }

    public sealed record HosterCapabilities(string Code, bool SupportsSpooledUpload, bool SupportsRemoteFetch);
}
