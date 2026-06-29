using MassTransit;
using Microsoft.Extensions.Logging;
using ReplicaGuard.Contracts.IntegrationEvents;
using ReplicaGuard.Domain.Replication;
using ReplicaGuard.Infrastructure.Messaging.Commands;
using ReplicaGuard.Infrastructure.Persistence;

namespace ReplicaGuard.Infrastructure.Messaging.Consumers;

public sealed class ReplicaTerminalIntegrationEventConsumer :
    IConsumer<ReplicaDownloadedIntegrationEvent>,
    IConsumer<ReplicaFailedIntegrationEvent>
{
    private readonly IAssetRepository _assets;
    private readonly ILogger<ReplicaTerminalIntegrationEventConsumer> _logger;

    public ReplicaTerminalIntegrationEventConsumer(
        IAssetRepository assets,
        ILogger<ReplicaTerminalIntegrationEventConsumer> logger)
    {
        _assets = assets;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ReplicaDownloadedIntegrationEvent> context) 
        => await WakeWaitingPeersAsync(context.Message.ReplicaId, context, true);

    public async Task Consume(ConsumeContext<ReplicaFailedIntegrationEvent> context)
        => await WakeWaitingPeersAsync(context.Message.ReplicaId, context);

    private async Task WakeWaitingPeersAsync(
        Guid sourceReplicaId,
        ConsumeContext context,
        bool wakeReplicaItself = true)
    {
        var asset = await _assets.GetByReplicaIdWithReplicasAsync(
            sourceReplicaId, context.CancellationToken);

        if (asset is null)
            return;

        var waitingPeers = asset.Replicas
            .Where(r => r.Status == ReplicaStatus.WaitingForPeer &&
                        r.WaitingForReplicaId == sourceReplicaId)
            .ToList();

        foreach (var peer in waitingPeers)
        {
            await context.Send(
                new UploadReplicaCommand(asset.UserId, asset.Id, peer.Id),
                context.CancellationToken);

            _logger.LogInformation(
                "Woke Replica {ReplicaId} (was waiting for {SiblingId})",
                peer.Id, sourceReplicaId);
        }

        if (wakeReplicaItself)
        {
            var replica = asset.Replicas.FirstOrDefault(r => r.Id == sourceReplicaId);
            if (replica is not null)
            {
                await context.Send(
                    new UploadReplicaCommand(asset.UserId, asset.Id, replica.Id),
                    context.CancellationToken);
                _logger.LogInformation(
                    "Woke Replica {ReplicaId} itself",
                    replica.Id);
            }
        }
    }
}

public sealed class ReplicaTerminalIntegrationEventConsumerDefinition
    : ConsumerDefinition<ReplicaTerminalIntegrationEventConsumer>
{
    public ReplicaTerminalIntegrationEventConsumerDefinition()
    {
        EndpointName = "replica-terminal"; // covers both downloaded + failed
        ConcurrentMessageLimit = 8;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<ReplicaTerminalIntegrationEventConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        consumerConfigurator.UseMessageRetry(r =>
        {
            r.Interval(3, TimeSpan.FromSeconds(5));
        });

        endpointConfigurator.UseEntityFrameworkOutbox<ApplicationDbContext>(context);
    }
}
