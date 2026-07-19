using Microsoft.Extensions.Logging;
using ReplicaGuard.Application.Abstractions.Clock;
using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Application.Replication.ProgressStreaming;
using ReplicaGuard.Application.Replication.UploadReplica.Fetching;
using ReplicaGuard.Application.Replication.UploadReplica.Spooling;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Capabilities;
using ReplicaGuard.Domain.HosterAccounts;
using ReplicaGuard.Domain.Hosters;
using ReplicaGuard.Domain.Replication;

namespace ReplicaGuard.Application.Replication.UploadReplica;

public sealed class UploadReplicaCommandHandler
    : ICommandHandler<UploadReplicaCommand>
{
    private readonly IReplicaRepository _replicas;
    private readonly IHosterRepository _hosters;
    private readonly IHosterDefinitionResolver _resolver;
    private readonly IHosterAccountRepository _accounts;
    private readonly IAssetRepository _assets;
    private readonly ICapabilityFactory _capabilityFactory;
    private readonly ISpoolLeaseService _leases;
    private readonly IFileFetcher _fileFetcher;
    private readonly ISpoolFileLocator _fileLocator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IReplicaEventStream _eventStream;
    private readonly ILogger<UploadReplicaCommandHandler> _logger;

    public UploadReplicaCommandHandler(
        IReplicaRepository replicas,
        IHosterRepository hosters,
        IHosterDefinitionResolver resolver,
        IHosterAccountRepository accounts,
        IAssetRepository assets,
        ICapabilityFactory capabilityFactory,
        ISpoolLeaseService leases,
        IFileFetcher fileFetcher,
        ISpoolFileLocator fileLocator,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        ILogger<UploadReplicaCommandHandler> logger,
        IReplicaEventStream eventStream)
    {
        _replicas = replicas;
        _hosters = hosters;
        _resolver = resolver;
        _accounts = accounts;
        _assets = assets;
        _capabilityFactory = capabilityFactory;
        _leases = leases;
        _fileFetcher = fileFetcher;
        _fileLocator = fileLocator;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
        _eventStream = eventStream;
    }

    public async Task<Result> Handle(UploadReplicaCommand cmd, CancellationToken ct)
    {
        var ctxResult = await LoadAndValidateAsync(cmd, ct);
        if (ctxResult.IsFailure)
            return Result.Failure(ctxResult.Error);

        var ctx = ctxResult.Value;

        if (ctx.Asset is null)
            return Result.Success();

        try
        {
            return ctx.Asset.Source switch
            {
                LocalFileSource local => await HandleLocalAsync(ctx, local, ct),
                RemoteFileSource remote => await HandleRemoteAsync(ctx, remote, ct),
                _ => throw new InvalidOperationException("Unknown source type")
            };
        }
        catch
        {
            ctx.Replica.MarkAsRetrying();

            await SaveAndPublishStateAsync(ctx.Asset, ctx.Replica, ct);
            throw;
        }
        finally
        {
            if (ctx.Asset is not null)
            {
                await _leases.ReleaseForAsset(ctx.Asset.Id);
            }
        }
    }

    private async Task<Result<UploadContext>> LoadAndValidateAsync(
        UploadReplicaCommand cmd,
        CancellationToken ct)
    {
        var replica = await _replicas.GetByIdAsync(cmd.ReplicaId, ct);
        if (replica is null)
            return Result.Failure<UploadContext>(ReplicationErrors.ReplicaNotFound(cmd.ReplicaId).AsPermanent());

        if (replica.IsTerminal)
            return Result.Success(new UploadContext(cmd, replica));

        var asset = await _assets.GetByIdWithReplicasAsync(cmd.AssetId, cmd.UserId, ct);
        if (asset is null)
        {
            _logger.LogWarning("Asset {AssetId} not found", cmd.AssetId);
            return Result.Failure<UploadContext>(ReplicationErrors.AssetNotFound(cmd.AssetId).AsPermanent());
        }

        var hoster = await _hosters.GetByIdAsync(replica.HosterId, ct);
        if (hoster is null)
        {
            _logger.LogWarning("HosterId {HosterId} not found", replica.HosterId);
            return Result.Failure<UploadContext>(HosterErrors.NotFound(replica.HosterId).AsPermanent());
        }

        var account = await _accounts.GetByIdAsync(replica.HosterAccountId!.Value, ct);
        if (account is null)
            return Result.Failure<UploadContext>(HosterAccountErrors.NotFound(replica.HosterAccountId!.Value).AsPermanent());

        var def = _resolver.Resolve(hoster.Code);
        if (def is null)
            return Result.Failure<UploadContext>(HosterErrors.NotFound(hoster.Id).AsPermanent());

        var capability = asset.Source switch
        {
            LocalFileSource => CapabilityCode.LocalFileUpload,
            RemoteFileSource => CapabilityCode.RemoteFileUpload,
            _ => throw new InvalidOperationException("Unknown source type")
        };

        var requirement = def.GetRequirement(capability);

        if (requirement is null && capability == CapabilityCode.RemoteFileUpload)
        {
            capability = CapabilityCode.LocalFileUpload;
            requirement = def.GetRequirement(CapabilityCode.LocalFileUpload);
        }

        if (requirement is null)
        {
            replica.MarkAsFailed();
            await SaveAndPublishStateAsync(asset, replica, ct);
            return Result.Failure<UploadContext>(
                HosterErrors.CapabilityNotSupported(def.Code, capability).AsPermanent());
        }

        var verified = account.Identities
            .Where(i => i.Status == IdentityVerificationStatus.Verified)
            .ToList();

        if (!requirement.IsSatisfiedBy(verified))
        {
            replica.MarkAsFailed();
            await SaveAndPublishStateAsync(asset, replica, ct);
            return Result.Failure<UploadContext>(
                HosterAccountErrors.RequiredIdentitiesNotSatisfied(
                    requirement,
                    def.Code,
                    capability).AsPermanent());
        }

        object? uploader = capability switch
        {
            CapabilityCode.LocalFileUpload => _capabilityFactory.Get<ILocalFileUploadHandler>(def.Code),
            CapabilityCode.RemoteFileUpload => _capabilityFactory.Get<IRemoteFileUploadHandler>(def.Code),
            _ => throw new InvalidOperationException("Unknown capability code")
        };

        if (uploader is null)
        {
            replica.MarkAsFailed();
            await SaveAndPublishStateAsync(asset, replica, ct);
            return Result.Failure<UploadContext>(
                UploadReplicaErrors.UploadNotSupported(def.Code.ToFriendlyString()));
        }

        var lease = await _leases.GetAsync(asset.Id, ct);
        var spool = SpoolStateResolver.Resolve(cmd.AssetId, asset.FileName.Value, _fileLocator, lease);

        return Result.Success(new UploadContext(
            cmd,
            asset,
            replica,
            hoster,
            uploader,
            account,
            def,
            spool,
            capability));
    }

    private async Task<Result> HandleLocalAsync(
        UploadContext ctx,
        LocalFileSource local,
        CancellationToken ct)
    {
        if (!File.Exists(local.FilePath))
        {
            _logger.LogWarning("Local file {FilePath} not found for asset {AssetId}", local.FilePath, ctx.Asset.Id);
            ctx.Replica.MarkAsFailed();
            await SaveAndPublishStateAsync(ctx.Asset, ctx.Replica, ct);
            return Result.Failure(UploadReplicaErrors.LocalFileNotFound(local.FilePath));
        }

        ctx.Replica.MarkAsUploading();
        await SaveAndPublishStateAsync(ctx.Asset, ctx.Replica, ct);

        long? totalBytes = ctx.Asset.SizeBytes ?? new FileInfo(local.FilePath).Length;
        var progressDelegate = CreateProgressDelegate(ctx, totalBytes);

        var handler = (ILocalFileUploadHandler)ctx.Uploader;
        var request = new LocalFileUploadRequest(ctx.Account, ctx.Asset.FileName.Value, local, progressDelegate);
        var result = await handler.HandleAsync(request, ct);

        if (result.IsFailure)
        {
            ctx.Replica.MarkAsFailed();
            await SaveAndPublishStateAsync(ctx.Asset, ctx.Replica, ct);
            return Result.Failure(result.Error);
        }

        ctx.Replica.MarkAsCompleted(result.Value.FileUrl);
        if (result.Value.SizeBytes is long bytes)
            ctx.Asset.RecordFileSize(bytes, _dateTimeProvider.UtcNow);

        await SaveAndPublishStateAsync(ctx.Asset, ctx.Replica, ct);
        return Result.Success();
    }

    private async Task<Result> HandleRemoteAsync(
        UploadContext ctx,
        RemoteFileSource source,
        CancellationToken ct)
    {
        if (ctx.Capability == CapabilityCode.RemoteFileUpload)
        {
            var handler = (IRemoteFileUploadHandler)ctx.Uploader;
            return await UploadFromRemoteUrlAsync(ctx, handler, source, ct);
        }

        return ctx.Spool.Status switch
        {
            SpoolStatus.NotExist => await SpoolAndDownloadAsync(ctx, source, ct),
            SpoolStatus.Downloading => WaitForSpool(ctx),
            SpoolStatus.Completed => await UploadFromSpoolAsync(ctx, ct),
            _ => throw new InvalidOperationException("Unknown spool status")
        };
    }

    private async Task<Result> UploadFromRemoteUrlAsync(
        UploadContext ctx,
        IRemoteFileUploadHandler handler,
        RemoteFileSource remoteSource,
        CancellationToken ct)
    {
        ctx.Replica.MarkAsUploading();
        await SaveAndPublishStateAsync(ctx.Asset, ctx.Replica, ct);

        var request = new RemoteFileUploadRequest(ctx.Account, ctx.Asset.FileName.Value, remoteSource, CreateProgressDelegate(ctx, ctx.Asset.SizeBytes));
        var result = await handler.HandleAsync(request, ct);

        if (result.IsFailure)
        {
            ctx.Replica.MarkAsRetrying();

            await SaveAndPublishStateAsync(ctx.Asset, ctx.Replica, ct);
            return Result.Failure(result.Error);
        }

        ctx.Replica.MarkAsCompleted(result.Value.FileUrl);
        if (result.Value.SizeBytes is long bytes)
            ctx.Asset.RecordFileSize(bytes, _dateTimeProvider.UtcNow);

        await SaveAndPublishStateAsync(ctx.Asset, ctx.Replica, ct);
        return Result.Success();
    }

    private async Task<Result> SpoolAndDownloadAsync(
        UploadContext ctx,
        RemoteFileSource remoteSource,
        CancellationToken ct)
    {
        var acquired = await _leases.TryAcquireAsync(
            ctx.Asset.Id,
            ctx.Replica.Id,
            TimeSpan.FromMinutes(30),
            ct);

        if (acquired is null)
        {
            var waitingResult = await _replicas.TryMarkWaitingIfDownloaderStillActive(
                ctx.Asset.Id,
                ctx.Replica.Id,
                ct);

            return waitingResult switch
            {
                MarkWaitingResult.AlreadyCompleted =>
                    await UploadFromSpoolAsync(
                        ctx with { Spool = SpoolStateResolver.Resolve(ctx.Cmd.AssetId, ctx.Asset.FileName.Value, _fileLocator, null) },
                        ct),

                MarkWaitingResult.MarkedWaiting =>
                    Result.Success(),

                MarkWaitingResult.NoActiveDownloader =>
                    Result.Failure(UploadReplicaErrors.DownloaderDisappeared),

                _ => throw new InvalidOperationException("Unknown waiting result")
            };
        }

        ctx.Replica.MarkAsDownloading();
        await SaveAndPublishStateAsync(ctx.Asset, ctx.Replica, ct);

        var progressDelegate = CreateProgressDelegate(ctx, ctx.Asset.SizeBytes);
        var fetched = await _fileFetcher.DownloadAsync(ctx.Asset.Id, ctx.Asset.FileName.Value, remoteSource, progressDelegate, ct);

        await _leases.ReleaseForAsset(ctx.Asset.Id);
        if (fetched.IsFailure)
        {
            ctx.Replica.MarkAsRetrying();
            await SaveAndPublishStateAsync(ctx.Asset, ctx.Replica, ct);
            return Result.Failure(fetched.Error);
        }

        ctx.Asset.RecordFileSize(fetched.Value.SizeBytes, _dateTimeProvider.UtcNow);
        ctx.Replica.MarkAsDownloaded();
        await SaveAndPublishStateAsync(ctx.Asset, ctx.Replica, ct);

        return Result.Success();
    }

    private async Task<Result> UploadFromSpoolAsync(
        UploadContext ctx,
        CancellationToken ct)
    {
        ctx.Replica.MarkAsUploading();
        await SaveAndPublishStateAsync(ctx.Asset, ctx.Replica, ct);

        long totalBytes = ctx.Asset.SizeBytes ?? new FileInfo(ctx.Spool.FilePath).Length;
        var progressDelegate = CreateProgressDelegate(ctx, totalBytes);

        var handler = (ILocalFileUploadHandler)ctx.Uploader;
        var localResult = LocalFileSource.Create(
            Path.GetDirectoryName(ctx.Spool.FilePath)!,
            Path.GetFileName(ctx.Spool.FilePath));
        if (localResult.IsFailure)
            return Result.Failure(localResult.Error);
        var request = new LocalFileUploadRequest(
            ctx.Account, 
            ctx.Asset.FileName.Value,
            localResult.Value, 
            progressDelegate);
        var result = await handler.HandleAsync(request, ct);

        if (result.IsFailure)
        {
            ctx.Replica.MarkAsRetrying();
            await SaveAndPublishStateAsync(ctx.Asset, ctx.Replica, ct);
            return Result.Failure(result.Error);
        }

        ctx.Replica.MarkAsCompleted(result.Value.FileUrl);
        await SaveAndPublishStateAsync(ctx.Asset, ctx.Replica, ct);

        return Result.Success();
    }

    private Result WaitForSpool(UploadContext ctx)
        => Result.Success();

    private async Task SaveAndPublishStateAsync(Asset asset, Replica replica, CancellationToken ct)
    {
        await _unitOfWork.SaveChangesAsync(ct);

        var evt = new ReplicaStreamEvent(
            ReplicaId: replica.Id,
            OccurredAtUtc: replica.UpdatedAtUtc,
            Status: replica.Status);

        _eventStream.Publish(asset.UserId, asset.Id, evt);
    }

    private Action<TransferProgress> CreateProgressDelegate(UploadContext ctx, long? totalBytes)
    {
        long lastTick = Environment.TickCount64;

        return progress =>
        {
            var now = Environment.TickCount64;
            // Throttle to 250ms, unless we've finished the stream.
            if (now - lastTick > 250 || (totalBytes.HasValue && progress.BytesTransferred == totalBytes.Value))
            {
                lastTick = now;
                var evt = new ReplicaStreamEvent(
                    ReplicaId: ctx.Replica.Id,
                    Status: ctx.Replica.Status,
                    OccurredAtUtc: _dateTimeProvider.UtcNow,
                    BytesTransferred: progress.BytesTransferred,
                    TotalBytes: totalBytes);

                // Fire and forget publish to channels
                _eventStream.Publish(ctx.Cmd.UserId, ctx.Asset.Id, evt);
            }
        };
    }

    public sealed record UploadContext(
        UploadReplicaCommand Cmd,
        Asset Asset,
        Replica Replica,
        Hoster Hoster,
        object Uploader,
        HosterAccount Account,
        IHosterDefinition Definition,
        SpoolState Spool,
        CapabilityCode Capability)
    {
        public UploadContext(UploadReplicaCommand cmd, Replica replica)
            : this(cmd, default!, replica, default!, default!, default!, default!, default!, default!) { }
    }
}
