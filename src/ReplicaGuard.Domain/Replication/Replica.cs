using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Replication.DomainEvents;

namespace ReplicaGuard.Domain.Replication;

public sealed class Replica : Entity<Guid>
{
    public Guid AssetId { get; private set; }
    public Guid HosterId { get; private set; }
    public Guid? HosterAccountId { get; private set; }
    public ReplicaStatus Status { get; set; }
    public Uri? Link { get; set; }
    public Guid? WaitingForReplicaId { get; set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private readonly List<ReplicaStatusUpdate> _statusUpdates = [];

    // EF Core
    private Replica() { }

    internal static Replica Create(Guid assetId, Guid hosterId, Guid? accountId, DateTime utcNow)
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
        RaiseDomainEvent(new ReplicaFailedDomainEvent(Id));
        RecordTransition(ReplicaStatus.Failed);
    }

    public void MarkAsCompleted(Uri link)
    {
        Link = link;
        RecordTransition(ReplicaStatus.Completed);
    }

    public void MarkAsWaitingForPeer(Guid peerReplicaId)
    {
        WaitingForReplicaId = peerReplicaId;
        RecordTransition(ReplicaStatus.WaitingForPeer);
    }

    public void MarkAsDownloading()
        => RecordTransition(ReplicaStatus.Downloading);

    public void MarkAsDownloaded()
    {
        RaiseDomainEvent(new ReplicaDownloadedDomainEvent(Id));
    }

    public void MarkAsUploading()
        => RecordTransition(ReplicaStatus.Uploading);

    public void MarkAsRetrying()
        => RecordTransition(ReplicaStatus.Retrying);

    public void ReportDownloadProgress(long bytesTransferred)
    {
        if (Status is not ReplicaStatus.Downloading)
        {
            throw new InvalidOperationException(
                "Cannot report transfer progress unless the replica is downloading.");
        }

        ReportTransferProgress(bytesTransferred);
    }

    public void ReportUploadProgress(long bytesTransferred)
    {
        if (Status is not ReplicaStatus.Uploading)
        {
            throw new InvalidOperationException(
                "Cannot report transfer progress unless the replica is uploading.");
        }

        ReportTransferProgress(bytesTransferred);
    }

    private void ReportTransferProgress(long bytesTransferred)
    {
        if (bytesTransferred < 0)
        {
            throw new Exception(
                "Bytes transferred cannot be negative.");
        }

        RecordTransition(
            Status,
            bytesTransferred);
    }

    private void RecordTransition(ReplicaStatus status, long? progress = null)
    {
        //EnsureTransitionIsValid(Status, newStatus);
        Status = status;
        UpdatedAtUtc = DateTime.UtcNow;

        _statusUpdates.Add(
            new ReplicaStatusUpdate(
                Id,
                Status,
                UpdatedAtUtc,
                progress));

        RaiseDomainEvent(new ReplicaStatusUpdatedDomainEvent(Id, Status, UpdatedAtUtc, progress));
    }

    internal void SetStatusForTesting(ReplicaStatus status)
    {
        Status = status;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
