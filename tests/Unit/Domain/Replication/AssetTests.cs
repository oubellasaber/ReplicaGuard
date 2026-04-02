using FluentAssertions;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Replication;
using ReplicaGuard.Core.Domain.Replication.DomainEvents;

namespace ReplicaGuard.Domain.Tests.Replication;

public class AssetTests
{
    [Fact]
    public void AddReplica_WithDuplicateHoster_ReturnsFailure()
    {
        // Arrange
        Asset asset = CreateRemoteAsset();
        Guid hosterId = Guid.NewGuid();

        Result<Replica> firstAddResult = asset.AddReplica(hosterId);
        firstAddResult.IsSuccess.Should().BeTrue();

        // Act
        Result<Replica> result = asset.AddReplica(hosterId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ReplicationErrors.DuplicateReplica(asset.Id, hosterId).Code);
    }

    [Fact]
    public void RecalculateState_WithDownloadingReplica_SetsDownloadingState()
    {
        // Arrange
        Asset asset = CreateRemoteAsset();
        Replica replica = AddReplica(asset);
        replica.MarkDownloading().IsSuccess.Should().BeTrue();

        // Act
        asset.RecalculateState();

        // Assert
        asset.State.Should().Be(AssetState.Downloading);
    }

    [Fact]
    public void RecalculateState_WithPendingReplica_SetsUploadingState()
    {
        // Arrange
        Asset asset = CreateRemoteAsset();
        AddReplica(asset);

        // Act
        asset.RecalculateState();

        // Assert
        asset.State.Should().Be(AssetState.Uploading);
    }

    [Fact]
    public void RecalculateState_WithAllReplicasFailed_SetsFailedState()
    {
        // Arrange
        Asset asset = CreateRemoteAsset();
        Replica firstReplica = AddReplica(asset);
        Replica secondReplica = AddReplica(asset);

        firstReplica.MarkFailed("first failure").IsSuccess.Should().BeTrue();
        secondReplica.MarkFailed("second failure").IsSuccess.Should().BeTrue();

        // Act
        asset.RecalculateState();

        // Assert
        asset.State.Should().Be(AssetState.Failed);
    }

    [Fact]
    public void RecalculateState_WhenAllReplicasCompleted_RaisesAllReplicasCompletedEvent()
    {
        // Arrange
        Asset asset = CreateRemoteAsset();
        Replica firstReplica = AddReplica(asset);
        Replica secondReplica = AddReplica(asset);

        firstReplica.MarkUploading().IsSuccess.Should().BeTrue();
        firstReplica.MarkCompleted(new Uri("https://example.com/first")).IsSuccess.Should().BeTrue();

        secondReplica.MarkUploading().IsSuccess.Should().BeTrue();
        secondReplica.MarkCompleted(new Uri("https://example.com/second")).IsSuccess.Should().BeTrue();

        // Act
        asset.RecalculateState();

        // Assert
        asset.State.Should().Be(AssetState.Completed);
        asset.GetDomainEvents().OfType<AllReplicasCompleted>().Should().ContainSingle();
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
