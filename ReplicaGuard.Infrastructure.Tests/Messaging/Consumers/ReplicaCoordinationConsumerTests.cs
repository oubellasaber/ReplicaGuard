using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ReplicaGuard.Core.Domain.Replication;
using ReplicaGuard.Core.Domain.Replication.DomainEvents;
using ReplicaGuard.Infrastructure.Messaging.Commands;
using ReplicaGuard.Infrastructure.Messaging.Consumers;
using ReplicaGuard.Infrastructure.Tests.Messaging.Consumers.Helpers;

namespace ReplicaGuard.Infrastructure.Tests.Messaging.Consumers;

public class ReplicaCoordinationConsumerTests
{
    private readonly IAssetRepository _assetRepository;
    private readonly ReplicaCoordinationConsumer _sut;

    public ReplicaCoordinationConsumerTests()
    {
        _assetRepository = Substitute.For<IAssetRepository>();

        _sut = new ReplicaCoordinationConsumer(
            _assetRepository,
            Substitute.For<ILogger<ReplicaCoordinationConsumer>>());
    }

    [Fact]
    public async Task Consume_ReplicaCompleted_WakesWaitingPeers()
    {
        // Arrange — Pixeldrain is waiting for SendCM
        Asset asset = TestData.CreateRemoteAsset(TestData.SendCmHosterId, TestData.PixeldrainHosterId);
        Replica sendCmReplica = asset.Replicas[0];
        Replica pixeldrainReplica = asset.Replicas[1];

        // Pixeldrain is waiting for SendCM
        pixeldrainReplica.MarkWaitingForPeer(sendCmReplica.Id);

        _assetRepository.GetByIdWithReplicasAsync(asset.Id, Arg.Any<CancellationToken>())
            .Returns(asset);

        var message = new ReplicaCompleted(
            sendCmReplica.Id,
            asset.Id,
            TestData.SendCmHosterId,
            new Uri("https://sendcm.com/file/123"));

        ConsumeContext<ReplicaCompleted> context = TestData.MockConsumeContext(message);

        // Act
        await _sut.Consume(context);

        // Assert — should publish UploadReplicaCommand for the waiting peer
        await context.Received(1).Publish(
            Arg.Is<UploadReplicaCommand>(cmd =>
                cmd.ReplicaId == pixeldrainReplica.Id &&
                cmd.AssetId == asset.Id &&
                cmd.HosterId == TestData.PixeldrainHosterId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_ReplicaFailed_WakesWaitingPeers()
    {
        // Arrange
        Asset asset = TestData.CreateRemoteAsset(TestData.SendCmHosterId, TestData.PixeldrainHosterId);
        Replica sendCmReplica = asset.Replicas[0];
        Replica pixeldrainReplica = asset.Replicas[1];

        pixeldrainReplica.MarkWaitingForPeer(sendCmReplica.Id);

        _assetRepository.GetByIdWithReplicasAsync(asset.Id, Arg.Any<CancellationToken>())
            .Returns(asset);

        var message = new ReplicaFailed(
            sendCmReplica.Id,
            asset.Id,
            TestData.SendCmHosterId,
            "Upload failed");

        ConsumeContext<ReplicaFailed> context = TestData.MockConsumeContext(message);

        // Act
        await _sut.Consume(context);

        // Assert
        await context.Received(1).Publish(
            Arg.Is<UploadReplicaCommand>(cmd => cmd.ReplicaId == pixeldrainReplica.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_NoPeersWaiting_PublishesNothing()
    {
        // Arrange
        Asset asset = TestData.CreateRemoteAsset(TestData.SendCmHosterId, TestData.PixeldrainHosterId);
        Replica sendCmReplica = asset.Replicas[0];

        // Nobody is waiting
        _assetRepository.GetByIdWithReplicasAsync(asset.Id, Arg.Any<CancellationToken>())
            .Returns(asset);

        var message = new ReplicaCompleted(
            sendCmReplica.Id, asset.Id, TestData.SendCmHosterId,
            new Uri("https://done.com/file"));

        ConsumeContext<ReplicaCompleted> context = TestData.MockConsumeContext(message);

        // Act
        await _sut.Consume(context);

        // Assert
        await context.DidNotReceive().Publish(
            Arg.Any<UploadReplicaCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_PeerWaitingForDifferentReplica_DoesNotWake()
    {
        // Arrange
        Guid thirdHosterId = Guid.NewGuid();
        Asset asset = TestData.CreateRemoteAsset(
            TestData.SendCmHosterId, TestData.PixeldrainHosterId, thirdHosterId);

        Replica sendCmReplica = asset.Replicas[0];
        Replica pixeldrainReplica = asset.Replicas[1];
        Replica thirdReplica = asset.Replicas[2];

        // Pixeldrain is waiting for the THIRD replica, not SendCM
        pixeldrainReplica.MarkWaitingForPeer(thirdReplica.Id);

        _assetRepository.GetByIdWithReplicasAsync(asset.Id, Arg.Any<CancellationToken>())
            .Returns(asset);

        var message = new ReplicaCompleted(
            sendCmReplica.Id, asset.Id, TestData.SendCmHosterId,
            new Uri("https://done.com/file"));

        ConsumeContext<ReplicaCompleted> context = TestData.MockConsumeContext(message);

        // Act
        await _sut.Consume(context);

        // Assert — Pixeldrain should NOT be woken
        await context.DidNotReceive().Publish(
            Arg.Is<UploadReplicaCommand>(cmd => cmd.ReplicaId == pixeldrainReplica.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_AssetNotFound_DoesNothing()
    {
        // Arrange
        _assetRepository.GetByIdWithReplicasAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Asset?)null);

        var message = new ReplicaCompleted(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new Uri("https://done.com/file"));

        ConsumeContext<ReplicaCompleted> context = TestData.MockConsumeContext(message);

        // Act
        await _sut.Consume(context);

        // Assert
        await context.DidNotReceive().Publish(
            Arg.Any<UploadReplicaCommand>(),
            Arg.Any<CancellationToken>());
    }
}
