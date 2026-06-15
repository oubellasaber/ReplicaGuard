using MassTransit;
using Microsoft.Extensions.Logging;
using ReplicaGuard.Contracts.IntegrationEvents;
using ReplicaGuard.Infrastructure.Messaging.Commands;
using ReplicaGuard.Infrastructure.Persistence;

namespace ReplicaGuard.Infrastructure.Messaging.Consumers;

public sealed class AssetCreatedIntegrationEventConsumer :
    IConsumer<AssetCreatedIntegrationEvent>
{
    private readonly ILogger<AssetCreatedIntegrationEventConsumer> _logger;

    public AssetCreatedIntegrationEventConsumer(
        ILogger<AssetCreatedIntegrationEventConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AssetCreatedIntegrationEvent> context)
    {
        var (userId, assetId, replicaIds) = context.Message;

        foreach (var replicaId in replicaIds)
        {
            await context.Send(
                new UploadReplicaCommand(userId, assetId, replicaId),
                context.CancellationToken);

            _logger.LogInformation(
                "Queued Replica {ReplicaId} for processing.",
                replicaId);
        }
    }
}

public sealed class AssetCreatedIntegrationEventConsumerDefinition
    : ConsumerDefinition<AssetCreatedIntegrationEventConsumer>
{
    public AssetCreatedIntegrationEventConsumerDefinition()
    {
        EndpointName = "asset-created";
        ConcurrentMessageLimit = 8;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<AssetCreatedIntegrationEventConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        consumerConfigurator.UseMessageRetry(r =>
        {
            r.Interval(3, TimeSpan.FromSeconds(5));
        });

        endpointConfigurator.UseEntityFrameworkOutbox<ApplicationDbContext>(context);
    }
}
