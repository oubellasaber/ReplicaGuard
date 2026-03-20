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

public class AssetCreatedConsumerTests
{
    private readonly IAssetRepository _assetRepository;
    private readonly AssetCreatedConsumer _sut;

    public AssetCreatedConsumerTests()
    {
        _assetRepository = Substitute.For<IAssetRepository>();

        _sut = new AssetCreatedConsumer(
            _assetRepository,
            Substitute.For<ILogger<AssetCreatedConsumer>>());
    }

    [Fact]
    public async Task Consume_PublishesCommandForEachPendingReplica()
    {
        // Arrange
        Asset asset = TestData.CreateRemoteAsset(TestData.SendCmHosterId, TestData.PixeldrainHosterId);

        _assetRepository.GetByIdWithReplicasAsync(asset.Id, Arg.Any<CancellationToken>())
            .Returns(asset);

        var message = new AssetCreated(asset.Id, TestData.UserId, "test-file.zip");
        ConsumeContext<AssetCreated> context = TestData.MockConsumeContext(message);

        // Act
        await _sut.Consume(context);

        // Assert
        await context.Received(2).Publish(
            Arg.Any<UploadReplicaCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_SkipsNonPendingReplicas()
    {
        // Arrange
        Asset asset = TestData.CreateRemoteAsset(TestData.SendCmHosterId, TestData.PixeldrainHosterId);

        // Mark first replica as already uploading
        asset.Replicas[0].MarkUploading();
        asset.Replicas[0].ClearDomainEvents();

        _assetRepository.GetByIdWithReplicasAsync(asset.Id, Arg.Any<CancellationToken>())
            .Returns(asset);

        var message = new AssetCreated(asset.Id, TestData.UserId, "test-file.zip");
        ConsumeContext<AssetCreated> context = TestData.MockConsumeContext(message);

        // Act
        await _sut.Consume(context);

        // Assert — only 1 command published (for the pending one)
        await context.Received(1).Publish(
            Arg.Any<UploadReplicaCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_AssetNotFound_DoesNothing()
    {
        // Arrange
        _assetRepository.GetByIdWithReplicasAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Asset?)null);

        var message = new AssetCreated(Guid.NewGuid(), TestData.UserId, "test.zip");
        ConsumeContext<AssetCreated> context = TestData.MockConsumeContext(message);

        // Act
        await _sut.Consume(context);

        // Assert
        await context.DidNotReceive().Publish(
            Arg.Any<UploadReplicaCommand>(),
            Arg.Any<CancellationToken>());
    }
}
