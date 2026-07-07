using ReplicaGuard.Domain.Replication;

namespace ReplicaGuard.Application.Replication.ProgressStreaming;

public sealed record ReplicaStreamEvent(
    Guid ReplicaId,
    DateTime OccurredAtUtc,
    ReplicaStatus Status,
    long? BytesTransferred = null,
    long? TotalBytes = null,
    long SequenceNumber = 0);
