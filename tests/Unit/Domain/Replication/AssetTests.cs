using FluentAssertions;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Replication;

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
}
