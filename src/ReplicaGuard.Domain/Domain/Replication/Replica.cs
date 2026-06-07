using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Replication.DomainEvents;

namespace ReplicaGuard.Core.Domain.Replication;

public sealed class Replica : Entity<Guid>
{
    public Guid AssetId { get; private set; }
    public Guid HosterId { get; private set; }
    public ReplicaStatus Status { get; set; }
    public Uri? Link { get; set; }
    public Guid? WaitingForReplicaId { get; set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; set; }

    // EF Core
    private Replica() : base(Guid.NewGuid()) { }

    internal static Replica Create(Guid assetId, Guid hosterId, DateTime utcNow)
    {
        var replica = new Replica
        {
            Id = Guid.NewGuid(),
            AssetId = assetId,
            HosterId = hosterId,
            Status = ReplicaStatus.Pending,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

        return replica;
    }

    public bool IsTerminal =>
        Status is ReplicaStatus.Completed or ReplicaStatus.Failed;

    public void MarkAsFailed(DateTime utcNow)
    {
        Status = ReplicaStatus.Failed;
        UpdatedAtUtc = utcNow;
        RaiseDomainEvent(new ReplicaFailed(Id));
    }

    public void MarkAsCompleted(Uri link, DateTime utcNow)
    {
        Status = ReplicaStatus.Completed;
        Link = link;
        UpdatedAtUtc = utcNow;
    }

    public void MarkAsWaitingForPeer(Guid peerReplicaId, DateTime utcNow)
    {
        Status = ReplicaStatus.WaitingForPeer;
        WaitingForReplicaId = peerReplicaId;
        UpdatedAtUtc = utcNow;
    }

    public void MarkAsDownloading(DateTime utcNow)
    {
        Status = ReplicaStatus.Downloading;
        UpdatedAtUtc = utcNow;
    }

    public void MarkAsDownloaded(DateTime utcNow)
    {
        RaiseDomainEvent(new ReplicaDownloaded(Id, utcNow));
    }

    public void MarkAsUploading(DateTime utcNow)
    {
        Status = ReplicaStatus.Uploading;
        UpdatedAtUtc = utcNow;
    }

    public void MarkAsRetrying(DateTime utcNow)
    {
        Status = ReplicaStatus.Retrying;
        UpdatedAtUtc = utcNow;
    }
}
