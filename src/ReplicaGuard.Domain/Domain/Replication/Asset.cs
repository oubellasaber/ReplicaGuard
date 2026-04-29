using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Hoster;
using ReplicaGuard.Core.Domain.Replication.DomainEvents;

namespace ReplicaGuard.Core.Domain.Replication;

public sealed class Asset : Entity<Guid>
{
    private readonly HashSet<Replica> _replicas = new();

    public Guid UserId { get; private set; }
    public FileSource? Source { get; private set; }
    public FileName FileName { get; private set; } = default!;
    public AssetState State { get; private set; }
    public long? SizeBytes { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public uint Version { get; private set; }

    public IReadOnlyCollection<Replica> Replicas => _replicas;

    // EF Core
    private Asset() : base(Guid.NewGuid()) { }

    /// <summary>
    /// Creates an asset from a remote Download URL.
    /// File needs to be downloaded first (Downloading state).
    /// </summary>
    public static Result<Asset> CreateFromRemoteUrl(
        Guid userId,
        RemoteFileSource source,
        FileName fileName)
    {
        Asset asset = new()
        {
            UserId = userId,
            Source = source,
            FileName = fileName,
            State = AssetState.Created,
            CreatedAtUtc = DateTime.UtcNow
        };

        asset.RaiseDomainEvent(new AssetCreated(asset.Id, userId, fileName.Value));

        return Result.Success(asset);
    }

    /// <summary>
    /// Creates an asset from a remote URL (convenience method).
    /// </summary>
    public static Result<Asset> CreateFromRemoteUrl(
        Guid userId,
        string url,
        FileName fileName)
    {
        Result<RemoteFileSource> sourceResult = RemoteFileSource.Create(url);
        if (sourceResult.IsFailure)
            return Result.Failure<Asset>(sourceResult.Error);

        return CreateFromRemoteUrl(userId, sourceResult.Value, fileName);
    }

    /// <summary>
    /// Creates an asset from a local file path on the user's computer.
    /// File is already accessible, no download needed (starts as Created, but can be immediately spooled).
    /// </summary>
    public static Result<Asset> CreateFromLocalPath(
        Guid userId,
        LocalFileSource source,
        FileName fileName)
    {
        Asset asset = new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Source = source,
            FileName = fileName,
            State = AssetState.Created,
            CreatedAtUtc = DateTime.UtcNow
        };

        asset.RaiseDomainEvent(new AssetCreated(asset.Id, userId, fileName.Value));

        return Result.Success(asset);
    }

    /// <summary>
    /// Creates an asset from a local file path (convenience method).
    /// </summary>
    public static Result<Asset> CreateFromLocalPath(
        Guid userId,
        string filePath,
        FileName fileName)
    {
        Result<LocalFileSource> sourceResult = LocalFileSource.Create(filePath);
        if (sourceResult.IsFailure)
            return Result.Failure<Asset>(sourceResult.Error);

        return CreateFromLocalPath(userId, sourceResult.Value, fileName);
    }

    public Result<Replica> AddReplica(Guid hosterId)
    {
        if (_replicas.Any(r => r.HosterId == hosterId))
            return Result.Failure<Replica>(
                ReplicationErrors.DuplicateReplica(Id, hosterId));

        Replica replica = Replica.Create(Id, hosterId);
        _replicas.Add(replica);
        RecalculateState();

        return Result.Success(replica);
    }

    /// <summary>
    /// Records the file size once it's known (from local disk or after download).
    /// Idempotent — only sets the value if not already known.
    /// </summary>
    public void RecordFileSize(long sizeBytes)
    {
        if (SizeBytes.HasValue || sizeBytes <= 0)
            return;

        SizeBytes = sizeBytes;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public Result StartDownloading(Replica replica)
    {
        var hashsetReplica = _replicas.FirstOrDefault(r => r.Id == replica.Id);
        if (hashsetReplica == null)
            return Result.Failure(new Error("Replica.NotFound", "Replica does not belong to this asset."));
        if (hashsetReplica.IsTerminal)
            return Result.Failure(new Error("Replica.TerminalState", "Replica is already in terminal state, forbidden transition."));
        hashsetReplica.State = ReplicaState.Downloading;
        Touch();
        RecalculateState();
        return Result.Success(replica);
    }

    public Result StartUploading(Replica replica)
    {
        var hashsetReplica = _replicas.FirstOrDefault(r => r.Id == replica.Id);
        if (hashsetReplica == null)
            return Result.Failure(new Error("Replica.NotFound", "Replica does not belong to this asset."));
        if (hashsetReplica.IsTerminal)
            return Result.Failure(new Error("Replica.TerminalState", "Replica is already in terminal state, forbidden transition."));
        hashsetReplica.State = ReplicaState.Uploading;
        Touch();
        RecalculateState();
        return Result.Success(replica);
    }

    public Result Complete(Replica replica, Uri fileUrl)
    {
        var hashsetReplica = _replicas.FirstOrDefault(r => r.Id == replica.Id);
        if (hashsetReplica == null)
            return Result.Failure(new Error("Replica.NotFound", "Replica does not belong to this asset."));
        if (hashsetReplica.IsTerminal)
            return Result.Failure(new Error("Replica.TerminalState", "Replica is already in terminal state, forbidden transition."));
        hashsetReplica.State = ReplicaState.Completed;
        hashsetReplica.Link = fileUrl;
        Touch();
        RecalculateState();
        RaiseDomainEvent(new ReplicaCompleted(replica.Id, Id, replica.HosterId, fileUrl));
        return Result.Success(replica);
    }

    public Result<FailureDecision> RecordFailure(Replica replica, Error error)
    {
        var hashsetReplica = _replicas.FirstOrDefault(r => r.Id == replica.Id);
        if (hashsetReplica == null)
            return Result.Failure<FailureDecision>(new Error("Replica.NotFound", "Replica does not belong to this asset."));
        if (hashsetReplica.IsTerminal)
            return Result.Failure<FailureDecision>(new Error("Replica.TerminalState", "Replica is already in terminal state, forbidden transition."));

        hashsetReplica.RetryCount++;
        hashsetReplica.LastError = error.Code;
        Touch();

        if (error.IsPermanent() || !replica.HasRetriesRemaining)
        {
            hashsetReplica.State = ReplicaState.Failed;
            RaiseDomainEvent(new ReplicaFailed(replica.Id, Id, replica.HosterId, error.Code));
            return Result.Success(FailureDecision.Permanent);
        }

        hashsetReplica.State = ReplicaState.Retrying;
        RecalculateState();
        return Result.Success(FailureDecision.Retryable);
    }

    public Result Fail(Replica replica, string errorCode)
    {
        var hashsetReplica = _replicas.FirstOrDefault(r => r.Id == replica.Id);
        if (hashsetReplica == null)
            return Result.Failure(new Error("Replica.NotFound", "Replica does not belong to this asset."));
        if (hashsetReplica.IsTerminal)
            return Result.Failure(new Error("Replica.TerminalState", "Replica is already in terminal state."));

        hashsetReplica.LastError = errorCode;
        hashsetReplica.State = ReplicaState.Failed;
        Touch();
        RecalculateState();
        RaiseDomainEvent(new ReplicaFailed(replica.Id, Id, replica.HosterId, errorCode));
        return Result.Success();
    }

    public Result MarkWaitingForPeer(Replica replica, Guid peerReplicaId)
    {
        var hashsetReplica = _replicas.FirstOrDefault(r => r.Id == replica.Id);
        if (hashsetReplica == null)
            return Result.Failure(new Error("Replica.NotFound", "Replica does not belong to this asset."));
        if (hashsetReplica.IsTerminal)
            return Result.Failure(new Error("Replica.TerminalState", "Replica is already in terminal state, forbidden transition."));

        hashsetReplica.State = ReplicaState.WaitingForPeer;
        hashsetReplica.WaitingForReplicaId = peerReplicaId;
        Touch();
        RecalculateState();
        return Result.Success();
    }

    private void RecalculateState()
    {
        if (_replicas.Count == 0 || _replicas.All(r => r.State == ReplicaState.Pending))
        {
            return;
        }

        if (_replicas.All(r => r.State == ReplicaState.Completed))
        {
            var prev = State;
            State = AssetState.Completed;
            if (prev != AssetState.Completed)
                RaiseDomainEvent(new AllReplicasCompleted(Id));
            return;
        }

        if (_replicas.All(r => r.State == ReplicaState.Failed))
        {
            State = AssetState.Failed;
            return;
        }

        State = AssetState.InProgress;
    }

    private void Touch()
    {
        UpdatedAtUtc = DateTime.UtcNow;
        Version++;
    }
}

public enum FailureDecision
{
    Permanent,
    Retryable
}
