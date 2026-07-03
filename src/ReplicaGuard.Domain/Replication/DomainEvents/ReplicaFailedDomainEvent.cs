using MediatR;
using ReplicaGuard.Contracts.IntegrationEvents;
using ReplicaGuard.Domain.Abstractions;

namespace ReplicaGuard.Domain.Replication.DomainEvents;

public sealed record ReplicaFailedDomainEvent(Guid ReplicaId) : IDomainEvent;

public sealed class ReplicaFailedDomainEventHandler(IIntegrationEventOutbox outbox)
        : INotificationHandler<ReplicaFailedDomainEvent>
{
    public async Task Handle(ReplicaFailedDomainEvent evt, CancellationToken ct)
    {
        var integrationEvent = new ReplicaFailedIntegrationEvent(evt.ReplicaId);
        await outbox.Add(integrationEvent);
    }
}
