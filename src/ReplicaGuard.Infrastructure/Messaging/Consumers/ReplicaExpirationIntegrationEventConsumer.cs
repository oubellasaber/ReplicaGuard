using MassTransit;
using Microsoft.Extensions.Logging;
using ReplicaGuard.Application.Replication.Recovery;
using ReplicaGuard.Contracts.IntegrationEvents;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Replication;
using ReplicaGuard.Infrastructure.Persistence;

namespace ReplicaGuard.Infrastructure.Messaging.Consumers;

public sealed class ReplicaExpirationIntegrationEventConsumer :
    IConsumer<ReplicaExpiredIntegrationEvent>,
    IConsumer<ReplicaExpiringSoonIntegrationEvent>
{
    private readonly IAssetRepository _assets;
    private readonly IReplicaRecoveryService _recovery;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReplicaExpirationIntegrationEventConsumer> _logger;

    public ReplicaExpirationIntegrationEventConsumer(
        IAssetRepository assets,
        IReplicaRecoveryService recovery,
        IUnitOfWork unitOfWork,
        ILogger<ReplicaExpirationIntegrationEventConsumer> logger)
    {
        _assets = assets;
        _recovery = recovery;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ReplicaExpiredIntegrationEvent> context)
    {
        _logger.LogWarning(
            "Replica {ReplicaId} expired on hoster {HosterId}",
            context.Message.ReplicaId, context.Message.HosterId);

        await Task.CompletedTask;
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

        // ToDo: This shoudl be fixed (Think about it???)
        replica.MarkAsTombstoned();
        await _unitOfWork.SaveChangesAsync(context.CancellationToken);
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
