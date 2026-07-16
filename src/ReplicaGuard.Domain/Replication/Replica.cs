using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Replication.DomainEvents;

namespace ReplicaGuard.Domain.Replication;

public sealed class Replica : Entity<Guid>
{
    private readonly List<ReplicaStatusTransition> _statusTransitions = new();

    public Guid AssetId { get; private set; }
    public Guid HosterId { get; private set; }
    public Guid? HosterAccountId { get; private set; }
    public ReplicaStatus Status { get; private set; }
    public Uri? Link { get; private set; }
    public Guid? WaitingForReplicaId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid? SourceReplicaId { get; private set; }
    public DateTime? PredictedExpiryAtUtc { get; private set; }
    public DateTime? LastExpirationCheckAtUtc { get; private set; }
    public ReplicaAvailabilityStatus AvailabilityStatus { get; private set; }

    public IReadOnlyCollection<ReplicaStatusTransition> StatusTransitions => _statusTransitions;

    // EF Core
    private Replica() { }

    internal static Replica Create(
        Guid assetId,
        Guid hosterId,
        Guid? accountId,
        DateTime utcNow)
    {
        var replica = new Replica
        {
            Id = Guid.NewGuid(),
            AssetId = assetId,
            HosterId = hosterId,
            HosterAccountId = accountId,
            Status = ReplicaStatus.Pending,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
            SourceReplicaId = null,
            PredictedExpiryAtUtc = null,
            LastExpirationCheckAtUtc = null,
            AvailabilityStatus = ReplicaAvailabilityStatus.Unknown
        };

        return replica;
    }

    internal static Replica CreateBackup(
        Guid assetId,
        Guid hosterId,
        Guid? accountId,
        Uri link,
        DateTime utcNow,
        Guid sourceReplicaId)
    {
        var replica = new Replica
        {
            Id = Guid.NewGuid(),
            AssetId = assetId,
            HosterId = hosterId,
            HosterAccountId = accountId,
            Status = ReplicaStatus.Completed,
            Link = link,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
            SourceReplicaId = sourceReplicaId,
            PredictedExpiryAtUtc = null,
            LastExpirationCheckAtUtc = null,
            AvailabilityStatus = ReplicaAvailabilityStatus.Unknown
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

    public void UpdateExpiry(
        DateTime expiryUtc,
        TimeSpan expiringSoonThreshold)
    {
        var nowUtc = DateTime.UtcNow;
        PredictedExpiryAtUtc = expiryUtc;
        LastExpirationCheckAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;

        var remaining = expiryUtc - nowUtc;

        var newStatus =
            remaining <= TimeSpan.Zero
                ? ReplicaAvailabilityStatus.Expired
                : remaining <= expiringSoonThreshold
                    ? ReplicaAvailabilityStatus.ExpiringSoon
                    : ReplicaAvailabilityStatus.Healthy;

        if (AvailabilityStatus == newStatus)
            return;

        AvailabilityStatus = newStatus;

        switch (newStatus)
        {
            case ReplicaAvailabilityStatus.Expired:
                RaiseDomainEvent(new ReplicaExpiredDomainEvent(Id, AssetId, HosterId));
                break;

            case ReplicaAvailabilityStatus.ExpiringSoon:
                RaiseDomainEvent(new ReplicaExpiringSoonDomainEvent(Id, AssetId, HosterId));
                break;
        }
    }

    public void MarkAsTombstoned()
    {
        AvailabilityStatus = ReplicaAvailabilityStatus.Tombstoned;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    //public void MarkAsExpired()
    //{
    //    AvailabilityStatus = ReplicaAvailabilityStatus.Expired;
    //    UpdatedAtUtc = DateTime.UtcNow;
    //    // raise a domain event
    //}

    //public void MarkAsExpiringSoon()
    //{
    //    AvailabilityStatus = ReplicaAvailabilityStatus.ExpiringSoon;
    //    UpdatedAtUtc = DateTime.UtcNow;
    //    // raise a domain event
    //}

    internal void SetStatusForTesting(ReplicaStatus status)
    {
        Status = status;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
