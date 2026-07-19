using MassTransit;
using Microsoft.Extensions.Logging;
using ReplicaGuard.Contracts.IntegrationEvents;
using ReplicaGuard.Domain.Replication;
using ReplicaGuard.Infrastructure.Persistence;
using ReplicaGuard.Infrastructure.Recovery;

namespace ReplicaGuard.Infrastructure.Messaging.Consumers;

public sealed class ReplicaExpirationIntegrationEventConsumer :
    IConsumer<ReplicaExpiredIntegrationEvent>,
    IConsumer<ReplicaExpiringSoonIntegrationEvent>
{
    private readonly IAssetRepository _assets;
    private readonly IReplicaRecoveryService _recovery;
    private readonly ILogger<ReplicaExpirationIntegrationEventConsumer> _logger;

    public ReplicaExpirationIntegrationEventConsumer(
        IAssetRepository assets,
        IReplicaRecoveryService recovery,
        ILogger<ReplicaExpirationIntegrationEventConsumer> logger)
    {
        _assets = assets;
        _recovery = recovery;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ReplicaExpiredIntegrationEvent> context)
    {
        var msg = context.Message;
        _logger.LogCritical("Replica {ReplicaId} expired on hoster {HosterId} — attempting recovery", msg.ReplicaId, msg.HosterId);

        var asset = await _assets.GetByReplicaIdWithReplicasAsync(msg.ReplicaId, context.CancellationToken);
        if (asset is null)
        {
            _logger.LogWarning("Asset not found for expired replica {ReplicaId}", msg.ReplicaId);
            return;
        }

        var replica = asset.Replicas.FirstOrDefault(r => r.Id == msg.ReplicaId);
        if (replica is null) return;

        await _recovery.Recover(asset, replica, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<ReplicaExpiringSoonIntegrationEvent> context)
    {
        var msg = context.Message;

        _logger.LogWarning(
            "Replica {ReplicaId} expiring soon on hoster {HosterId} — attempting recovery",
            msg.ReplicaId, msg.HosterId);

        var asset = await _assets.GetByReplicaIdWithReplicasAsync(
            msg.ReplicaId, context.CancellationToken);

        if (asset is null)
        {
            _logger.LogWarning(
                "Asset not found for expired replica {ReplicaId}", msg.ReplicaId);
            return;
        }

        var replica = asset.Replicas.FirstOrDefault(r => r.Id == msg.ReplicaId);
        if (replica is null)
        {
            _logger.LogWarning(
                "Replica {ReplicaId} not found on asset {AssetId}", msg.ReplicaId, msg.AssetId);
            return;
        }

        await _recovery.Recover(asset, replica, context.CancellationToken);
    }
}

public sealed class ReplicaExpirationIntegrationEventConsumerDefinition
    : ConsumerDefinition<ReplicaExpirationIntegrationEventConsumer>
{
    public ReplicaExpirationIntegrationEventConsumerDefinition()
    {
        EndpointName = "replica-expiration";
        ConcurrentMessageLimit = 4;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<ReplicaExpirationIntegrationEventConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        consumerConfigurator.UseMessageRetry(r =>
        {
            r.Interval(3, TimeSpan.FromSeconds(5));
        });

        endpointConfigurator.UseEntityFrameworkOutbox<ApplicationDbContext>(context);
    }
}
