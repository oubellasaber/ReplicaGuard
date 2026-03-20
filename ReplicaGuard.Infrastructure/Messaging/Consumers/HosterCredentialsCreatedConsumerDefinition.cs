using MassTransit;
using ReplicaGuard.Core.Domain.Credentials;
using ReplicaGuard.Infrastructure.Persistence;

namespace ReplicaGuard.Infrastructure.Messaging.Consumers;

public sealed class HosterCredentialsCreatedConsumerDefinition
    : ConsumerDefinition<HosterCredentialsCreatedConsumer>
{
    public HosterCredentialsCreatedConsumerDefinition()
    {
        EndpointName = "hoster-credentials-created";
        ConcurrentMessageLimit = 5;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<HosterCredentialsCreatedConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r =>
        {
            r.Exponential(
                retryLimit: 5,
                minInterval: TimeSpan.FromSeconds(1),
                maxInterval: TimeSpan.FromMinutes(2),
                intervalDelta: TimeSpan.FromSeconds(3));
            r.Handle<HttpRequestException>();
        });

        endpointConfigurator.UseEntityFrameworkOutbox<ApplicationDbContext>(context);
    }
}
