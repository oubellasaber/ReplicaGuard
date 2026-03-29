using MassTransit;

namespace ReplicaGuard.Infrastructure.Messaging.Consumers;

public sealed class UploadReplicaConsumerDefinition : ConsumerDefinition<UploadReplicaConsumer>
{
    public UploadReplicaConsumerDefinition()
    {
        ConcurrentMessageLimit = 5;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<UploadReplicaConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r => r.Exponential(
            retryLimit: 3,
            minInterval: TimeSpan.FromSeconds(5),
            maxInterval: TimeSpan.FromMinutes(2),
            intervalDelta: TimeSpan.FromSeconds(10)));

        endpointConfigurator.UseTimeout(t => t.Timeout = TimeSpan.FromMinutes(30));
    }
}
