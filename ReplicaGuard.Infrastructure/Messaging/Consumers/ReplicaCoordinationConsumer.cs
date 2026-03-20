using MassTransit;
using Microsoft.Extensions.Logging;
using ReplicaGuard.Core.Domain.Replication;
using ReplicaGuard.Core.Domain.Replication.DomainEvents;
using ReplicaGuard.Infrastructure.Messaging.Commands;

namespace ReplicaGuard.Infrastructure.Messaging.Consumers;

/// <summary>
/// When a replica completes or fails, re-triggers any siblings that were waiting for it.
/// </summary>
public sealed class ReplicaCoordinationConsumer :
    IConsumer<ReplicaCompleted>,
    IConsumer<ReplicaFailed>
{
    private readonly IAssetRepository _assetRepository;
    private readonly ILogger<ReplicaCoordinationConsumer> _logger;

    public ReplicaCoordinationConsumer(
        IAssetRepository assetRepository,
        ILogger<ReplicaCoordinationConsumer> logger)
    {
        _assetRepository = assetRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ReplicaCompleted> context)
    {
        await WakeWaitingPeersAsync(
            context.Message.AssetId, context.Message.ReplicaId, context);
    }

    public async Task Consume(ConsumeContext<ReplicaFailed> context)
    {
        await WakeWaitingPeersAsync(
            context.Message.AssetId, context.Message.ReplicaId, context);
    }

    private async Task WakeWaitingPeersAsync(
        Guid assetId, Guid sourceReplicaId, ConsumeContext context)
    {
        Asset? asset = await _assetRepository.GetByIdWithReplicasAsync(
            assetId, context.CancellationToken);

        if (asset == null)
            return;

        List<Replica> waitingPeers = asset.Replicas
            .Where(r => r.State == ReplicaState.WaitingForPeer &&
                       r.WaitingForReplicaId == sourceReplicaId)
            .ToList();

        foreach (Replica peer in waitingPeers)
        {
            await context.Publish(
                new UploadReplicaCommand(peer.Id, assetId, peer.HosterId),
                context.CancellationToken);

            _logger.LogInformation("Woke Replica {ReplicaId} (was waiting for {SiblingId})",
                peer.Id, sourceReplicaId);
        }
    }
}
