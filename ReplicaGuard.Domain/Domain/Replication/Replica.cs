using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Replication.DomainEvents;

namespace ReplicaGuard.Core.Domain.Replication;

public sealed class Replica : Entity<Guid>
{
    private const int MaxRetries = 3;

    public Guid AssetId { get; private set; }
    public Guid HosterId { get; private set; }
    public ReplicaState State { get; private set; }
    public Uri? Link { get; private set; }
    public string? LastError { get; private set; }
    public Guid? WaitingForReplicaId { get; private set; }
    public int RetryCount { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    // EF Core
    private Replica() : base(Guid.NewGuid()) { }

    internal static Replica Create(Guid assetId, Guid hosterId)
    {
        return new Replica
        {
            Id = Guid.NewGuid(),
            AssetId = assetId,
            HosterId = hosterId,
            State = ReplicaState.Pending,
            RetryCount = 0,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public Result MarkWaitingForPeer(Guid peerReplicaId)
    {
        bool canTransition =
            State == ReplicaState.Pending ||
            (State == ReplicaState.Failed && CanRetry());

        if (!canTransition)
            return Result.Failure(ReplicationErrors.InvalidReplicaStateTransition(State, ReplicaState.WaitingForPeer));

        State = ReplicaState.WaitingForPeer;
        WaitingForReplicaId = peerReplicaId;
        UpdatedAtUtc = DateTime.UtcNow;
        return Result.Success();
    }

    public Result MarkUploading()
    {
        bool canTransition =
            State == ReplicaState.Pending ||
            State == ReplicaState.WaitingForPeer ||
            (State == ReplicaState.Failed && CanRetry());

        if (!canTransition)
            return Result.Failure(ReplicationErrors.InvalidReplicaStateTransition(State, ReplicaState.Uploading));

        State = ReplicaState.Uploading;
        WaitingForReplicaId = null;
        LastError = null;
        UpdatedAtUtc = DateTime.UtcNow;
        return Result.Success();
    }

    public Result MarkCompleted(Uri link)
    {
        if (State != ReplicaState.Uploading)
            return Result.Failure(ReplicationErrors.InvalidReplicaStateTransition(State, ReplicaState.Completed));

        if (link == null)
            return Result.Failure(ReplicationErrors.LinkEmpty);

        State = ReplicaState.Completed;
        Link = link;
        LastError = null;
        WaitingForReplicaId = null;
        UpdatedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new ReplicaCompleted(Id, AssetId, HosterId, link));
        return Result.Success();
    }

    public Result MarkFailed(string reason)
    {
        if (State != ReplicaState.Uploading &&
            State != ReplicaState.Pending &&
            State != ReplicaState.WaitingForPeer &&
            State != ReplicaState.Failed)
            return Result.Failure(ReplicationErrors.InvalidReplicaStateTransition(State, ReplicaState.Failed));

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure(ReplicationErrors.ErrorReasonEmpty);

        State = ReplicaState.Failed;
        LastError = reason;
        WaitingForReplicaId = null;
        RetryCount++;
        UpdatedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new ReplicaFailed(Id, AssetId, HosterId, reason));
        return Result.Success();
    }

    public bool CanRetry() =>
        State == ReplicaState.Failed && RetryCount < MaxRetries;
}
