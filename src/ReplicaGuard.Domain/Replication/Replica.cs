using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Hosters;
using ReplicaGuard.Core.Replication.DomainEvents;

namespace ReplicaGuard.Core.Replication;

public sealed class Replica : Entity<Guid>
{
    public Guid AssetId { get; private set; }
    public HosterCode HosterId { get; private set; }
    public Guid? HosterAccountId { get; private set; }
    public ReplicaStatus Status { get; set; }
    public Uri? Link { get; set; }
    public Guid? WaitingForReplicaId { get; set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    // EF Core
    private Replica() : base(Guid.NewGuid()) { }

    internal static Replica Create(Guid assetId, HosterCode hosterId, Guid? accountId, DateTime utcNow)
    {
        var replica = new Replica
        {
            Id = Guid.NewGuid(),
            AssetId = assetId,
            HosterId = hosterId,
            HosterAccountId = accountId,
            Status = ReplicaStatus.Pending,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

        return replica;
    }

    public bool IsTerminal =>
        Status is ReplicaStatus.Completed or ReplicaStatus.Failed;

    public void MarkAsFailed()
    {
        Status = ReplicaStatus.Failed;
        UpdatedAtUtc = DateTime.UtcNow;
        RaiseDomainEvent(new ReplicaFailedDomainEvent(Id));
    }

    public void MarkAsCompleted(Uri link)
    {
        Status = ReplicaStatus.Completed;
        Link = link;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkAsWaitingForPeer(Guid peerReplicaId)
    {
        Status = ReplicaStatus.WaitingForPeer;
        WaitingForReplicaId = peerReplicaId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkAsDownloading()
    {
        Status = ReplicaStatus.Downloading;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkAsDownloaded()
    {
        RaiseDomainEvent(new ReplicaDownloadedDomainEvent(Id));
    }

    public void MarkAsUploading()
    {
        Status = ReplicaStatus.Uploading;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkAsRetrying()
    {
        Status = ReplicaStatus.Retrying;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    internal void SetStatusForTesting(ReplicaStatus status)
    {
        Status = status;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
