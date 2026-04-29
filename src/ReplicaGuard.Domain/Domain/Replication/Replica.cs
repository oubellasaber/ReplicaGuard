using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Replication.DomainEvents;

namespace ReplicaGuard.Core.Domain.Replication;

public sealed class Replica : Entity<Guid>
{
    private const int MaxRetries = 3;

    public Guid AssetId { get; private set; }
    public Guid HosterId { get; private set; }
    public ReplicaState State { get; internal set; } // TODO (rename): Status
    public Uri? Link { get; internal set; }
    public string? LastError { get; internal set; }
    public Guid? WaitingForReplicaId { get; internal set; }
    public int RetryCount { get; internal set; } // TODO (rename): AttemptCount
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; internal set; }

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

    public bool IsTerminal =>
        State is ReplicaState.Completed or ReplicaState.Failed;

    internal bool HasRetriesRemaining =>
        RetryCount < MaxRetries;

    public bool CanRetry() => HasRetriesRemaining;
}
