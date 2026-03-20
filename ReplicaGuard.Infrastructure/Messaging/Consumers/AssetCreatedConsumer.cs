using MassTransit;
using Microsoft.Extensions.Logging;
using ReplicaGuard.Core.Domain.Replication;
using ReplicaGuard.Core.Domain.Replication.DomainEvents;
using ReplicaGuard.Infrastructure.Messaging.Commands;

namespace ReplicaGuard.Infrastructure.Messaging.Consumers;

public sealed class AssetCreatedConsumer(
    IAssetRepository assetRepository,
    ILogger<AssetCreatedConsumer> logger) : IConsumer<AssetCreated>
{
    public async Task Consume(ConsumeContext<AssetCreated> context)
    {
        Asset? asset = await assetRepository.GetByIdWithReplicasAsync(
            context.Message.AssetId, context.CancellationToken);

        if (asset == null)
            return;

        foreach (Replica replica in asset.Replicas.Where(r => r.State == ReplicaState.Pending))
        {
            await context.Publish(
                new UploadReplicaCommand(replica.Id, asset.Id, replica.HosterId),
                context.CancellationToken);

            logger.LogInformation("Queued Replica {ReplicaId} -> Hoster {HosterId}",
                replica.Id, replica.HosterId);
        }
    }
}
