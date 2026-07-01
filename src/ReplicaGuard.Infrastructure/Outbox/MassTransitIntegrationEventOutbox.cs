using MassTransit;
using ReplicaGuard.Domain.Abstractions;

namespace ReplicaGuard.Infrastructure.Outbox;

public sealed class MassTransitIntegrationEventOutbox : IIntegrationEventOutbox
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitIntegrationEventOutbox(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public async Task Add<T>(T integrationEvent)
        where T : class
    {
        // This does NOT publish immediately.
        // MassTransit intercepts this call and writes the event
        // into the Outbox table inside the current EF Core transaction.
        await _publishEndpoint.Publish(integrationEvent, typeof(T), default);
    }
}
