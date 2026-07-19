using ReplicaGuard.Domain.Replication;
using ReplicaGuard.Domain.Replication.DomainEvents;

namespace ReplicaGuard.Domain.Tests;

public sealed class AssetTests
{
    private static readonly string s_fileName = "file.bin";
    private static readonly string s_filePath = @"/home/user/file.bin";
    private static readonly string s_remoteUrl = "https://example.com/file.bin";
    private static readonly string s_baseDirectory = "/base/";
    
    private static Asset CreateAsset(
        params (Guid hosterId, Guid? accountId, ReplicaStatus status)[] replicas)
    {
        var hosterIds = replicas.Select(r => (r.hosterId, r.accountId));
        var result = Asset.CreateFromRemoteUrl(
            Guid.NewGuid(),
            RemoteFileSource.Create(s_remoteUrl).Value,
            FileName.Create(s_fileName).Value,
            hosterIds);

        var asset = result.Value;

        foreach (var (hosterId, _, status) in replicas)
        {
            var replica = asset.Replicas.First(r => r.HosterId == hosterId);
            replica.SetStatusForTesting(status);
        }

        return asset;
    }

    private static Asset CreateAsset()
    {
        return CreateAsset([(Guid.NewGuid(), null, ReplicaStatus.Pending)]);
    }

    [Fact]
    public void asset_created_with_remote_url_has_expected_properties()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var result = Asset.CreateFromRemoteUrl(
            userId,
            s_remoteUrl,
            FileName.Create(s_fileName).Value,
            new[] { (Guid.NewGuid(), (Guid?)null) });

        // Assert
        Assert.True(result.IsSuccess);
        var sut = result.Value;
        Assert.Equal(userId, sut.UserId);
        Assert.Equal(s_fileName, sut.FileName.Value);
        Assert.True(sut.Source.IsRemote);
        Assert.Single(sut.Replicas);
    }

    [Fact]
    public void asset_created_from_local_path_has_expected_properties()
    {
        var userId = Guid.NewGuid();
        
        var result = Asset.CreateFromLocalPath(
            userId,
            s_baseDirectory,
            s_filePath,
            FileName.Create(s_fileName).Value,
            new[] { (Guid.NewGuid(), (Guid?)null) });

        Assert.True(result.IsSuccess);
        var sut = result.Value;
        Assert.Equal(userId, sut.UserId);
        Assert.True(sut.Source.IsLocal);
    }

    [Fact]
    public void creation_fails_when_remote_url_is_invalid()
    {
        var result = Asset.CreateFromRemoteUrl(
            Guid.NewGuid(),
            "not-a-url",
            FileName.Create(s_fileName).Value,
            new[] { (Guid.NewGuid(), (Guid?)null) });

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void creation_fails_when_duplicate_hoster_ids_are_provided()
    {
        var hosterId = Guid.NewGuid();
        var replicas = new[] { (hosterId, (Guid?)null), (hosterId, (Guid?)null) };

        var result = Asset.CreateFromRemoteUrl(
            Guid.NewGuid(),
            s_remoteUrl,
            FileName.Create(s_fileName).Value,
            replicas);

        Assert.True(result.IsFailure);
        Assert.Equal(ReplicationErrors.DuplicateReplica(default, hosterId).Code, result.Error.Code);
    }

    [Fact]
    public void creation_fails_when_no_replicas_exist()
    {
        var result = Asset.CreateFromRemoteUrl(
            Guid.NewGuid(),
            s_remoteUrl,
            FileName.Create(s_fileName).Value,
            Enumerable.Empty<(Guid, Guid?)>());

        Assert.True(result.IsFailure);
        Assert.Equal(ReplicationErrors.AssetHasNoReplicas(default).Code, result.Error.Code);
    }

    [Fact]
    public void status_is_created_when_all_replicas_are_pending()
    {
        var sut = CreateAsset(
            (Guid.NewGuid(), null, ReplicaStatus.Pending),
            (Guid.NewGuid(), null, ReplicaStatus.Pending));

        Assert.Equal(AssetStatus.Created, sut.Status);
    }

    [Fact]
    public void status_is_completed_when_all_replicas_are_completed()
    {
        var sut = CreateAsset(
            (Guid.NewGuid(), null, ReplicaStatus.Completed),
            (Guid.NewGuid(), null, ReplicaStatus.Completed));

        Assert.Equal(AssetStatus.Completed, sut.Status);
    }

    [Fact]
    public void status_is_failed_when_all_replicas_are_failed()
    {
        var sut = CreateAsset(
            (Guid.NewGuid(), null, ReplicaStatus.Failed),
            (Guid.NewGuid(), null, ReplicaStatus.Failed));

        Assert.Equal(AssetStatus.Failed, sut.Status);
    }

    [Fact]
    public void status_is_in_progress_when_replicas_have_mixed_states()
    {
        var sut = CreateAsset(
            (Guid.NewGuid(), null, ReplicaStatus.Pending),
            (Guid.NewGuid(), null, ReplicaStatus.Completed));

        Assert.Equal(AssetStatus.InProgress, sut.Status);
    }

    [Fact]
    public void record_file_size_sets_value_only_once()
    {
        var sut = CreateAsset();

        sut.RecordFileSize(1000, DateTime.UtcNow);
        sut.RecordFileSize(2000, DateTime.UtcNow.AddMinutes(1));

        Assert.NotNull(sut.SizeBytes);
        Assert.Equal(1000, sut.SizeBytes.Value);
    }

    [Fact]
    public void record_file_size_ignores_non_positive_values()
    {
        var sut = CreateAsset();

        sut.RecordFileSize(-5, DateTime.UtcNow);
        sut.RecordFileSize(0, DateTime.UtcNow);

        Assert.Null(sut.SizeBytes);
    }

    [Fact]
    public void mark_for_cleanup_sets_timestamp()
    {
        var sut = CreateAsset();
        var before = DateTime.UtcNow;

        sut.MarkForCleanup();

        Assert.NotNull(sut.CleanupAfterUtc);
        Assert.True(sut.CleanupAfterUtc >= before);
    }

    [Fact]
    public void mark_for_cleanup_is_idempotent()
    {
        var sut = CreateAsset();

        sut.MarkForCleanup();
        var first = sut.CleanupAfterUtc;
        sut.MarkForCleanup();

        Assert.Equal(first, sut.CleanupAfterUtc);
    }

    [Fact]
    public void clear_cleanup_removes_timestamp()
    {
        var sut = CreateAsset();
        sut.MarkForCleanup();
        Assert.NotNull(sut.CleanupAfterUtc);

        sut.ClearCleanup();

        Assert.Null(sut.CleanupAfterUtc);
    }

    [Fact]
    public void asset_creation_raises_domain_event_with_replica_ids()
    {
        var sut = CreateAsset();

        var domainEvent = sut.GetDomainEvents().OfType<AssetCreatedDomainEvent>().Single();

        Assert.Single(domainEvent.ReplicasIds);
    }

    [Fact]
    public void add_replica_backup_adds_completed_replica()
    {
        var sut = CreateAsset();
        var hosterId = Guid.NewGuid();
        var sourceReplicaId = sut.Replicas.First().Id;

        var backupResult = sut.AddReplicaBackup(
            hosterId,
            null,
            new Uri("https://backup.example.com/file.bin"),
            DateTime.UtcNow,
            sourceReplicaId);

        Assert.True(backupResult.IsSuccess);
        var backup = backupResult.Value;
        Assert.Equal(ReplicaStatus.Completed, backup.Status);
        Assert.Equal(sourceReplicaId, backup.SourceReplicaId);
    }
}
