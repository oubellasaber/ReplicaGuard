using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Replication.DomainEvents;

namespace ReplicaGuard.Domain.Replication;

public sealed class Asset : Entity<Guid>
{
    private readonly HashSet<Replica> _replicas = new();

    public Guid UserId { get; private set; }
    public FileSource Source { get; private set; } = null!;
    public FileName FileName { get; private set; } = default!;
    // Calculated
    public AssetStatus Status => CalculateStatus();
    public long? SizeBytes { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<Replica> Replicas => _replicas;

    // EF Core
    private Asset() : base(Guid.NewGuid()) { }

    private Asset(
        Guid userId,
        FileSource source,
        FileName fileName,
        DateTime createdAtUtc)
        : base(Guid.NewGuid())
    {
        UserId = userId;
        Source = source;
        FileName = fileName;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    private static Result<Asset> Create(
        Guid userId,
        FileSource source,
        FileName fileName,
        DateTime createdAtUtc,
        IEnumerable<(Guid hosterId, Guid? accountId)> replicas)
    {
        Asset asset = new Asset(userId, source, fileName, createdAtUtc);

        foreach (var (hosterId, accountId) in replicas)
        {
            var addResult = asset.AddReplica(hosterId, accountId, createdAtUtc);
            if (addResult.IsFailure)
                return Result.Failure<Asset>(addResult.Error);
        }

        asset.RaiseDomainEvent(new AssetCreatedDomainEvent(userId, asset.Id, asset.Replicas.Select(r => r.Id).ToList()));

        return asset;
    }

    /// <summary>
    /// Creates an asset from a remote Download URL.
    /// File needs to be downloaded first (Downloading state).
    /// </summary>
    public static Result<Asset> CreateFromRemoteUrl(
        Guid userId,
        RemoteFileSource source,
        FileName fileName,
        IEnumerable<(Guid hosterId, Guid? accountId)> replicas)
    {
        var result = Create(userId, source, fileName, DateTime.UtcNow, replicas);
        if (result.IsFailure)
            return Result.Failure<Asset>(result.Error);

        return Result.Success(result.Value);
    }

    /// <summary>
    /// Creates an asset from a remote URL (convenience method).
    /// </summary>
    public static Result<Asset> CreateFromRemoteUrl(
        Guid userId,
        string url,
        FileName fileName,
        IEnumerable<(Guid hosterId, Guid? accountId)> replicas)
    {
        Result<RemoteFileSource> sourceResult = RemoteFileSource.Create(url);
        if (sourceResult.IsFailure)
            return Result.Failure<Asset>(sourceResult.Error);

        return CreateFromRemoteUrl(userId, sourceResult.Value, fileName, replicas);
    }

    /// <summary>
    /// Creates an asset from a local file path on the user's computer.
    /// File is already accessible, no download needed (starts as Created, but can be immediately uploaded).
    /// </summary>
    public static Result<Asset> CreateFromLocalPath(
        Guid userId,
        LocalFileSource source,
        FileName fileName,
        IEnumerable<(Guid hosterId, Guid? accountId)> replicas)
    {
        var result = Create(userId, source, fileName, DateTime.UtcNow, replicas);
        if (result.IsFailure)
            return Result.Failure<Asset>(result.Error);

        return Result.Success(result.Value);
    }

    /// <summary>
    /// Creates an asset from a local file path (convenience method).
    /// </summary>
    public static Result<Asset> CreateFromLocalPath(
        Guid userId,
        string filePath,
        FileName fileName,
        IEnumerable<(Guid hosterId, Guid? accountId)> replicas)
    {
        Result<LocalFileSource> sourceResult = LocalFileSource.Create(filePath);
        if (sourceResult.IsFailure)
            return Result.Failure<Asset>(sourceResult.Error);

        return CreateFromLocalPath(userId, sourceResult.Value, fileName, replicas);
    }

    private Result<Replica> AddReplica(Guid hosterId, Guid? accountId, DateTime utcNow)
    {
        if (_replicas.Any(r => r.HosterId == hosterId))
            return Result.Failure<Replica>(
                ReplicationErrors.DuplicateReplica(Id, hosterId));

        Replica replica = Replica.Create(Id, hosterId, accountId, utcNow);
        _replicas.Add(replica);

        return Result.Success(replica);
    }

    /// <summary>
    /// Records the file size once it's known (from local disk or after download).
    /// Idempotent - only sets the value if not already known.
    /// </summary>
    public void RecordFileSize(long sizeBytes, DateTime utcNow)
    {
        if (SizeBytes.HasValue || sizeBytes <= 0)
            return;

        SizeBytes = sizeBytes;
        UpdatedAtUtc = utcNow;
    }

    private AssetStatus CalculateStatus()
    {
        if (!_replicas.Any() || _replicas.All(r => r.Status == ReplicaStatus.Pending))
            return AssetStatus.Created;
        else if (_replicas.All(r => r.Status == ReplicaStatus.Completed))
            return AssetStatus.Completed;
        else if (_replicas.All(r => r.Status == ReplicaStatus.Failed))
            return AssetStatus.Failed;
        else
            return AssetStatus.InProgress;
    }
}
