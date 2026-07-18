using ReplicaGuard.Domain.Abstractions;

namespace ReplicaGuard.Domain.Replication;

public class ReplicaStatusTransition : Entity<Guid>
{
    public Guid ReplicaId { get; private set; }
    public ReplicaStatus Status { get; private set; }
    public DateTime OccurredAt { get; private set; }

    private ReplicaStatusTransition() { }

    internal ReplicaStatusTransition(
        Guid replicaId,
        ReplicaStatus status,
        DateTime occurredAt) : base(Guid.NewGuid())
    {
        ReplicaId = replicaId;
        Status = status;
        OccurredAt = occurredAt;
    }
}
