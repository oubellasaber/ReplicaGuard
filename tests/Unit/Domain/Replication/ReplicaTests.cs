using FluentAssertions;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Replication;
using ReplicaGuard.Core.Domain.Replication.DomainEvents;

namespace ReplicaGuard.Domain.Tests.Replication;

public class ReplicaTests
{
    [Fact]
    public void MarkAttemptFailed_OnThirdFailure_TransitionsToFailedAndRaisesDomainEvent()
    {
        // Arrange
        Replica replica = CreatePendingReplica();

        replica.MarkAttemptFailed("first").IsSuccess.Should().BeTrue();
        replica.MarkAttemptFailed("second").IsSuccess.Should().BeTrue();

        // Act
        Result result = replica.MarkAttemptFailed("third");

        // Assert
        result.IsSuccess.Should().BeTrue();
        replica.State.Should().Be(ReplicaState.Failed);
        replica.RetryCount.Should().Be(3);
        replica.LastError.Should().Be("third");
        replica.GetDomainEvents().OfType<ReplicaFailed>().Should().ContainSingle();
    }

    [Fact]
    public void MarkCompleted_WhenReplicaIsNotUploading_ReturnsFailure()
    {
        // Arrange
        Replica replica = CreatePendingReplica();

        // Act
        Result result = replica.MarkCompleted(new Uri("https://example.com/file"));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ReplicationErrors.InvalidReplicaStateTransition(ReplicaState.Pending, ReplicaState.Completed).Code);
        replica.State.Should().Be(ReplicaState.Pending);
    }

    private static Replica CreatePendingReplica()
    {
        Result<FileName> fileNameResult = FileName.Create("archive.zip");
        fileNameResult.IsSuccess.Should().BeTrue();

        Result<Asset> assetResult = Asset.CreateFromRemoteUrl(
            Guid.NewGuid(),
            "https://example.com/archive.zip",
            fileNameResult.Value);

        assetResult.IsSuccess.Should().BeTrue();

        Result<Replica> replicaResult = assetResult.Value.AddReplica(Guid.NewGuid());
        replicaResult.IsSuccess.Should().BeTrue();

        return replicaResult.Value;
    }
}
