using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Replication.DomainEvents;

namespace ReplicaGuard.Core.Domain.Replication;

public sealed class Asset : Entity<Guid>
{
    private readonly List<Replica> _replicas = new();

    public Guid UserId { get; private set; }
    public FileSource? Source { get; private set; }
    public FileName FileName { get; private set; } = default!;
    public AssetState State { get; private set; }
    public long? SizeBytes { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public IReadOnlyList<Replica> Replicas => _replicas.AsReadOnly();

    // EF Core
    private Asset() : base(Guid.NewGuid()) { }

    /// <summary>
    /// Creates an asset from a remote URL.
    /// File needs to be downloaded first (Created -> Spooled states).
    /// </summary>
    public static Result<Asset> CreateFromRemoteUrl(
        Guid userId,
        RemoteFileSource source,
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

    public void RecalculateState()
    {
        if (_replicas.Count == 0)
            return;

        // A replica implies progress if it's explicitly in progress, OR failed but can still be retried.
        bool anyInProgress = _replicas.Any(r =>
            r.State is ReplicaState.Uploading or ReplicaState.Pending or ReplicaState.WaitingForPeer ||
            (r.State == ReplicaState.Failed && r.CanRetry()));
            
        // We only consider the overall asset failed if EVERY replica is permanently failed (no retries left)
        bool allPermanentlyFailed = _replicas.All(r => r.State == ReplicaState.Failed && !r.CanRetry());
        bool anyCompleted = _replicas.Any(r => r.State == ReplicaState.Completed);

        AssetState previousState = State;

        State = (anyInProgress, allPermanentlyFailed, anyCompleted) switch
        {
            (true, _, _) => AssetState.Uploading,
            (false, true, _) => AssetState.Failed,
            (false, false, true) => AssetState.Completed,
            _ => State
        };

        UpdatedAtUtc = DateTime.UtcNow;

        if (State == AssetState.Completed && previousState != AssetState.Completed)
        {
            RaiseDomainEvent(new AllReplicasCompleted(Id, UserId));
        }
    }
}
