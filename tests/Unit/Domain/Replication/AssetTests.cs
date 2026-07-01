using ReplicaGuard.Core.Hosters;
using ReplicaGuard.Core.Replication;

namespace ReplicaGuard.Domain.Tests.Replication;

using System;
using System.Linq;
using ReplicaGuard.Core.Replication;
using ReplicaGuard.Core.Hosters;
using ReplicaGuard.Core.Replication.DomainEvents;
using Xunit;

public sealed class AssetTests
{
    // ------------------------------------------------------------
    // Factory Helpers
    // ------------------------------------------------------------
    private static Asset CreateAsset(
        params (HosterCode hoster, Guid? accountId, ReplicaStatus status)[] replicas)
    {
        var result = Asset.CreateFromRemoteUrl(
            Guid.NewGuid(),
            new RemoteFileSource("https://example.com/file.bin"),
            new FileName("file.bin"),
            replicas.Select(r => (r.hoster, r.accountId))
        );

        var asset = result.Value;

        // Mutate replica statuses through public Replica API
        foreach (var (hoster, _, status) in replicas)
        {
            var replica = asset.Replicas.First(r => r.HosterId == hoster);
            replica.SetStatus(status, DateTime.UtcNow);
        }

        return asset;
    }

    // ============================================================
    // 1. STATUS CALCULATION (5 tests)
    // ============================================================

    [Fact]
    public void test_status_is_created_when_no_replicas_exist()
    {
        // Arrange
        var result = Asset.CreateFromRemoteUrl(
            Guid.NewGuid(),
            new RemoteFileSource("https://example.com/a"),
            new FileName("a.bin"),
            Enumerable.Empty<(HosterCode, Guid?)>()
        );

        var sut = result.Value;

        // Act
        var status = sut.Status;

        // Assert
        Assert.Equal(AssetStatus.Created, status);
    }

    [Fact]
    public void test_status_is_created_when_all_replicas_are_pending()
    {
        // Arrange
        var sut = CreateAsset(
            (HosterCode.GoogleDrive, null, ReplicaStatus.Pending),
            (HosterCode.Mega, null, ReplicaStatus.Pending)
        );

        // Act
        var status = sut.Status;

        // Assert
        Assert.Equal(AssetStatus.Created, status);
    }

    [Fact]
    public void test_status_is_completed_when_all_replicas_are_completed()
    {
        // Arrange
        var sut = CreateAsset(
            (HosterCode.GoogleDrive, null, ReplicaStatus.Completed),
            (HosterCode.Mega, null, ReplicaStatus.Completed)
        );

        // Act
        var status = sut.Status;

        // Assert
        Assert.Equal(AssetStatus.Completed, status);
    }

    [Fact]
    public void test_status_is_failed_when_all_replicas_are_failed()
    {
        // Arrange
        var sut = CreateAsset(
            (HosterCode.GoogleDrive, null, ReplicaStatus.Failed),
            (HosterCode.Mega, null, ReplicaStatus.Failed)
        );

        // Act
        var status = sut.Status;

        // Assert
        Assert.Equal(AssetStatus.Failed, status);
    }

    [Fact]
    public void test_status_is_in_progress_when_replicas_have_mixed_states()
    {
        // Arrange
        var sut = CreateAsset(
            (HosterCode.GoogleDrive, null, ReplicaStatus.Pending),
            (HosterCode.Mega, null, ReplicaStatus.Completed)
        );

        // Act
        var status = sut.Status;

        // Assert
        Assert.Equal(AssetStatus.InProgress, status);
    }

    // ============================================================
    // 2. DUPLICATE REPLICA PREVENTION (1 test)
    // ============================================================

    [Fact]
    public void test_creation_fails_when_duplicate_replicas_are_provided()
    {
        // Arrange
        var replicas = new[]
        {
            (HosterCode.GoogleDrive, (Guid?)null),
            (HosterCode.GoogleDrive, (Guid?)null) // duplicate
        };

        // Act
        var result = Asset.CreateFromRemoteUrl(
            Guid.NewGuid(),
            new RemoteFileSource("https://example.com/file.bin"),
            new FileName("file.bin"),
            replicas
        );

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(
            ReplicationErrors.DuplicateReplica(default, HosterCode.GoogleDrive).Code,
            result.Error.Code
        );
    }

    // ============================================================
    // 3. DOMAIN EVENT EMISSION (1 test)
    // ============================================================

    [Fact]
    public void test_asset_creation_raises_domain_event_with_all_replica_ids()
    {
        // Arrange
        var result = Asset.CreateFromRemoteUrl(
            Guid.NewGuid(),
            new RemoteFileSource("https://example.com/a"),
            new FileName("a.bin"),
            new[]
            {
                (HosterCode.GoogleDrive, (Guid?)null),
                (HosterCode.Mega, (Guid?)null)
            }
        );

        var sut = result.Value;

        // Act
        var domainEvent = sut.DomainEvents.OfType<AssetCreatedDomainEvent>().Single();

        // Assert
        Assert.Equal(2, domainEvent.ReplicaIds.Count);
    }

    // ============================================================
    // 4. FILE SIZE RECORDING (2 tests)
    // ============================================================

    [Fact]
    public void test_record_file_size_sets_value_only_once()
    {
        // Arrange
        var sut = CreateAsset();

        // Act
        sut.RecordFileSize(1000, DateTime.UtcNow);
        sut.RecordFileSize(2000, DateTime.UtcNow.AddMinutes(1));

        // Assert
        Assert.Equal(1000, sut.SizeBytes);
    }

    [Fact]
    public void test_record_file_size_ignores_non_positive_values()
    {
        // Arrange
        var sut = CreateAsset();

        // Act
        sut.RecordFileSize(-5, DateTime.UtcNow);
        sut.RecordFileSize(0, DateTime.UtcNow);

        // Assert
        Assert.Null(sut.SizeBytes);
    }
}
