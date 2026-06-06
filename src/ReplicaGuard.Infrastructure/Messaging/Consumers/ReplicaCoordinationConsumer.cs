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
    IConsumer<ReplicaDownloaded>,
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

    public async Task Consume(ConsumeContext<ReplicaDownloaded> context)
    {
        await WakeWaitingPeersAsync(context.Message.ReplicaId, context);
    }

    public async Task Consume(ConsumeContext<ReplicaFailed> context)
    {
        await WakeWaitingPeersAsync(context.Message.ReplicaId, context);
    }

    private async Task WakeWaitingPeersAsync(Guid sourceReplicaId, ConsumeContext context)
    {
        Asset? asset = await _assetRepository.GetByReplicaIdWithReplicasAsync(
            sourceReplicaId, context.CancellationToken);

        if (asset == null)
            return;

        List<Replica> waitingPeers = asset.Replicas
            .Where(r => r.Status == ReplicaStatus.WaitingForPeer &&
                       r.WaitingForReplicaId == sourceReplicaId)
            .ToList();

        foreach (Replica peer in waitingPeers)
        {
            await context.Publish(
                new UploadReplicaCommand(peer.Id, asset.Id, peer.HosterId),
                context.CancellationToken);

            _logger.LogInformation("Woke Replica {ReplicaId} (was waiting for {SiblingId})",
                peer.Id, sourceReplicaId);
        }
    }
}
