using ReplicaGuard.Domain.Replication;
using ReplicaGuard.Domain.Replication.DomainEvents;

namespace ReplicaGuard.Domain.Tests;

public sealed class ReplicaTests
{
    private static Replica CreateDefaultReplica()
    {
        return Replica.Create(Guid.NewGuid(), Guid.NewGuid(), null, DateTime.UtcNow);
    }

    [Fact]
    public void create_returns_pending_replica()
    {
        var assetId = Guid.NewGuid();
        var hosterId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var replica = Replica.Create(assetId, hosterId, accountId, DateTime.UtcNow);

        Assert.Equal(ReplicaStatus.Pending, replica.Status);
        Assert.Equal(assetId, replica.AssetId);
        Assert.Equal(hosterId, replica.HosterId);
        Assert.Equal(accountId, replica.HosterAccountId);
        Assert.Null(replica.SourceReplicaId);
        Assert.Null(replica.Link);
    }

    [Fact]
    public void create_backup_returns_completed_replica_with_link()
    {
        var assetId = Guid.NewGuid();
        var hosterId = Guid.NewGuid();
        var link = new Uri("https://example.com/file.bin");
        var sourceId = Guid.NewGuid();

        var replica = Replica.CreateBackup(assetId, hosterId, null, link, DateTime.UtcNow, sourceId);

        Assert.Equal(ReplicaStatus.Completed, replica.Status);
        Assert.Equal(link, replica.Link);
        Assert.Equal(sourceId, replica.SourceReplicaId);
    }

    [Fact]
    public void is_terminal_true_for_completed()
    {
        var replica = CreateDefaultReplica();
        replica.SetStatusForTesting(ReplicaStatus.Completed);

        Assert.True(replica.IsTerminal);
    }

    [Fact]
    public void is_terminal_true_for_failed()
    {
        var replica = CreateDefaultReplica();
        replica.SetStatusForTesting(ReplicaStatus.Failed);

        Assert.True(replica.IsTerminal);
    }

    [Fact]
    public void is_terminal_false_for_non_terminal_status()
    {
        var replica = CreateDefaultReplica();

        Assert.False(replica.IsTerminal);
    }

    [Fact]
    public void mark_as_failed_sets_status_and_raises_events()
    {
        var replica = CreateDefaultReplica();

        replica.MarkAsFailed();

        Assert.Equal(ReplicaStatus.Failed, replica.Status);
        Assert.Contains(replica.GetDomainEvents(), e => e is ReplicaFailedDomainEvent);
        Assert.Contains(replica.GetDomainEvents(), e => e is ReplicaTerminalStateReachedDomainEvent);
    }

    [Fact]
    public void mark_as_completed_sets_status_and_link_and_raises_terminal_event()
    {
        var replica = CreateDefaultReplica();
        var link = new Uri("https://example.com/uploaded.bin");

        replica.MarkAsCompleted(link);

        Assert.Equal(ReplicaStatus.Completed, replica.Status);
        Assert.Equal(link, replica.Link);
        Assert.Contains(replica.GetDomainEvents(), e => e is ReplicaTerminalStateReachedDomainEvent);
    }

    [Fact]
    public void mark_as_waiting_for_peer_sets_reference()
    {
        var replica = CreateDefaultReplica();
        var peerId = Guid.NewGuid();

        replica.MarkAsWaitingForPeer(peerId);

        Assert.Equal(ReplicaStatus.WaitingForPeer, replica.Status);
        Assert.Equal(peerId, replica.WaitingForReplicaId);
    }

    [Fact]
    public void mark_as_downloading_transitions_status()
    {
        var replica = CreateDefaultReplica();

        replica.MarkAsDownloading();

        Assert.Equal(ReplicaStatus.Downloading, replica.Status);
    }

    [Fact]
    public void mark_as_downloaded_raises_event()
    {
        var replica = CreateDefaultReplica();

        replica.MarkAsDownloaded();

        Assert.Contains(replica.GetDomainEvents(), e => e is ReplicaDownloadedDomainEvent);
    }

    [Fact]
    public void mark_as_uploading_transitions_status()
    {
        var replica = CreateDefaultReplica();

        replica.MarkAsUploading();

        Assert.Equal(ReplicaStatus.Uploading, replica.Status);
    }

    [Fact]
    public void mark_as_retrying_transitions_status()
    {
        var replica = CreateDefaultReplica();

        replica.MarkAsRetrying();

        Assert.Equal(ReplicaStatus.Retrying, replica.Status);
    }

    [Fact]
    public void update_expiry_with_far_future_sets_healthy()
    {
        var replica = CreateDefaultReplica();

        replica.UpdateExpiry(DateTime.UtcNow.AddDays(30), TimeSpan.FromDays(7));

        Assert.Equal(ReplicaAvailabilityStatus.Healthy, replica.AvailabilityStatus);
        Assert.DoesNotContain(replica.GetDomainEvents(), e => e is ReplicaExpiredDomainEvent);
        Assert.DoesNotContain(replica.GetDomainEvents(), e => e is ReplicaExpiringSoonDomainEvent);
    }

    [Fact]
    public void update_expiry_with_near_future_sets_expiring_soon_and_raises_event()
    {
        var replica = CreateDefaultReplica();

        replica.UpdateExpiry(DateTime.UtcNow.AddDays(1), TimeSpan.FromDays(7));

        Assert.Equal(ReplicaAvailabilityStatus.ExpiringSoon, replica.AvailabilityStatus);
        Assert.Contains(replica.GetDomainEvents(), e => e is ReplicaExpiringSoonDomainEvent);
    }

    [Fact]
    public void update_expiry_with_past_date_sets_expired_and_raises_event()
    {
        var replica = CreateDefaultReplica();

        replica.UpdateExpiry(DateTime.UtcNow.AddDays(-1), TimeSpan.FromDays(7));

        Assert.Equal(ReplicaAvailabilityStatus.Expired, replica.AvailabilityStatus);
        Assert.Contains(replica.GetDomainEvents(), e => e is ReplicaExpiredDomainEvent);
    }

    [Fact]
    public void update_expiry_does_not_raise_duplicate_event_for_same_status()
    {
        var replica = CreateDefaultReplica();

        replica.UpdateExpiry(DateTime.UtcNow.AddDays(-1), TimeSpan.FromDays(7));
        var eventCount = replica.GetDomainEvents().OfType<ReplicaExpiredDomainEvent>().Count();

        replica.UpdateExpiry(DateTime.UtcNow.AddDays(-2), TimeSpan.FromDays(7));

        // Still only one expired event
        Assert.Single(replica.GetDomainEvents().OfType<ReplicaExpiredDomainEvent>());
    }

    [Fact]
    public void mark_as_tombstoned_sets_status()
    {
        var replica = CreateDefaultReplica();

        replica.MarkAsTombstoned();

        Assert.Equal(ReplicaAvailabilityStatus.Tombstoned, replica.AvailabilityStatus);
    }
}
