using ReplicaGuard.Domain.Abstractions;

namespace ReplicaGuard.Domain.Replication;

public class ReplicaStatusUpdate : Entity<Guid>
{
    public Guid ReplicaId { get; private set; }
    public ReplicaStatus Status { get; private set; }
    public long? TransferredBytes { get; private set; }
    public DateTime OccurredAt { get; private set; }

    private ReplicaStatusUpdate() { }

    internal ReplicaStatusUpdate(
        Guid replicaId,
        ReplicaStatus status,
        DateTime occurredAt,
        long? transferredBytes)
    {
        ReplicaId = replicaId;
        Status = status;
        OccurredAt = occurredAt;

        if (status is ReplicaStatus.Downloading or ReplicaStatus.Uploading)
        {
            TransferredBytes = transferredBytes;
        }
    }
}
