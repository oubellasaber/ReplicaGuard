using MassTransit;
using ReplicaGuard.Application.Replication.ProgressStreaming;
using ReplicaGuard.Contracts.IntegrationEvents;
using ReplicaGuard.Domain.Replication;

namespace ReplicaGuard.Infrastructure.Messaging.Consumers;

public sealed class AssetCompletedConsumer
    : IConsumer<AssetCompletedIntegrationEvent>
{
    private readonly IAssetRepository _assets;
    private readonly IReplicaEventStream _stream;

    public AssetCompletedConsumer(
        IAssetRepository assets,
        IReplicaEventStream stream)
    {
        _assets = assets;
        _stream = stream;
    }

    public async Task Consume(
        ConsumeContext<AssetCompletedIntegrationEvent> context)
    {
        var msg = context.Message;

        //
        // 1. Re-check current truth from DB
        //
        var asset =
            await _assets.GetByIdAsync(msg.AssetId, context.CancellationToken);

        if (asset is null)
            return;

        //
        // 2. Guard: only proceed if STILL terminal
        // (prevents stale / duplicate events from acting)
        //
        if (asset.Status is not AssetStatus.Completed or AssetStatus.Failed)
            return;

        //
        // 3. Idempotent SSE completion
        //
        _stream.Publish(
            msg.ReplicaId,
            msg.AssetId,
            new ReplicaStreamEvent(
                ReplicaId: msg.ReplicaId,
                OccurredAtUtc: DateTime.UtcNow,
                Status: ReplicaStatus.Completed));

        _stream.CompleteAsset(
            asset.UserId,
            msg.AssetId);
    }
}
