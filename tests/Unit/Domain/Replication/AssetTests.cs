using FluentAssertions;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Replication;
using ReplicaGuard.Core.Domain.Replication.DomainEvents;

namespace ReplicaGuard.Domain.Tests.Replication;

public class AssetTests
{
    [Fact]
    public void add_replica_fails_when_hoster_already_exists()
    {
        // Arrange
        Asset sut = CreateRemoteAsset();
        Guid hosterId = Guid.NewGuid();

        sut.AddReplica(hosterId).IsSuccess.Should().BeTrue();

        // Act
        Result<Replica> result = sut.AddReplica(hosterId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ReplicationErrors.DuplicateReplica(sut.Id, hosterId).Code);
    }

    [Fact]
    public void starting_download_moves_asset_into_in_progress()
    {
        // Arrange
        Asset sut = CreateRemoteAsset();
        Replica replica = AddReplica(sut);

        // Act
        Result result = sut.StartDownloading(replica);

        // Assert
        result.IsSuccess.Should().BeTrue();
        replica.State.Should().Be(ReplicaState.Downloading);
        sut.State.Should().Be(AssetState.InProgress);
    }

    [Fact]
    public void asset_becomes_failed_when_all_replicas_fail()
    {
        // Arrange
        Asset sut = CreateRemoteAsset();
        Replica r1 = AddReplica(sut);
        Replica r2 = AddReplica(sut);

        Error e1 = new("FirstFailure", "first failure");
        Error e2 = new("SecondFailure", "second failure");

        // Act
        sut.Fail(r1, e1.Code).IsSuccess.Should().BeTrue();
        sut.Fail(r2, e2.Code).IsSuccess.Should().BeTrue();

        // Assert
        r1.State.Should().Be(ReplicaState.Failed);
        r2.State.Should().Be(ReplicaState.Failed);
        sut.State.Should().Be(AssetState.Failed);
    }

    [Fact]
    public void completing_all_replicas_sets_asset_completed_and_raises_event()
    {
        // Arrange
        Asset sut = CreateRemoteAsset();
        Replica r1 = AddReplica(sut);
        Replica r2 = AddReplica(sut);

        Uri link1 = new("https://example.com/first");
        Uri link2 = new("https://example.com/second");

        // Act
        sut.StartUploading(r1).IsSuccess.Should().BeTrue();
        sut.Complete(r1, link1).IsSuccess.Should().BeTrue();

        sut.StartUploading(r2).IsSuccess.Should().BeTrue();
        sut.Complete(r2, link2).IsSuccess.Should().BeTrue();

        // Assert
        sut.State.Should().Be(AssetState.Completed);
        sut.GetDomainEvents().OfType<AllReplicasCompleted>().Should().ContainSingle();
    }

    private static Asset CreateRemoteAsset()
    {
        Result<FileName> fileNameResult = FileName.Create("movie.mp4");
        fileNameResult.IsSuccess.Should().BeTrue();

        Result<Asset> assetResult = Asset.CreateFromRemoteUrl(
            Guid.NewGuid(),
            "https://example.com/movie.mp4",
            fileNameResult.Value);

        assetResult.IsSuccess.Should().BeTrue();
        return assetResult.Value;
    }

    private static Replica AddReplica(Asset asset)
    {
        Result<Replica> result = asset.AddReplica(Guid.NewGuid());
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }
}
