using MediatR;
using ReplicaGuard.Contracts.IntegrationEvents;
using ReplicaGuard.Domain.Abstractions;

namespace ReplicaGuard.Domain.Replication.DomainEvents;

public sealed record ReplicaDownloadedDomainEvent(Guid ReplicaId) : IDomainEvent;

public sealed class ReplicaDownloadedDomainEventHandler(IIntegrationEventOutbox outbox)
        : INotificationHandler<ReplicaDownloadedDomainEvent>
{
    public async Task Handle(ReplicaDownloadedDomainEvent evt, CancellationToken ct)
    {
        var integrationEvent = new ReplicaDownloadedIntegrationEvent(evt.ReplicaId);
        await outbox.Add(integrationEvent);
    }
}
