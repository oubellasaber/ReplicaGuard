using System;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Replication.DomainEvents;

namespace ReplicaGuard.Domain.Replication;

public sealed class Replica : Entity<Guid>
{
    private readonly List<ReplicaStatusTransition> _statusTransitions = new();

    public Guid AssetId { get; private set; }
    public Guid HosterId { get; private set; }
    public Guid? HosterAccountId { get; private set; }
    public ReplicaStatus Status { get; set; }
    public Uri? Link { get; set; }
    public Guid? WaitingForReplicaId { get; set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<ReplicaStatusTransition> StatusTransitions => _statusTransitions;

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
        RecordTransition(ReplicaStatus.Failed);
        RaiseDomainEvent(new ReplicaFailedDomainEvent(Id));
        RaiseDomainEvent(
        new ReplicaTerminalStateReachedDomainEvent(
            Id,
            AssetId,
            Status));
    }

    public void MarkAsCompleted(Uri link)
    {
        Link = link;
        RecordTransition(ReplicaStatus.Completed);
        RaiseDomainEvent(
        new ReplicaTerminalStateReachedDomainEvent(
            Id,
            AssetId,
            Status));
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

    private void RecordTransition(ReplicaStatus status)
    {
        Status = status;
        UpdatedAtUtc = DateTime.UtcNow;
        _statusTransitions.Add(
            new ReplicaStatusTransition(
                Id,
                status,
                UpdatedAtUtc));
    }

    internal void SetStatusForTesting(ReplicaStatus status)
    {
        Status = status;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
