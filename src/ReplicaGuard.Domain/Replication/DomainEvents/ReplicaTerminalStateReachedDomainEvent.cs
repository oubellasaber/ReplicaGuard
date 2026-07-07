using MediatR;
using ReplicaGuard.Contracts.IntegrationEvents;
using ReplicaGuard.Domain.Abstractions;

namespace ReplicaGuard.Domain.Replication.DomainEvents;

public sealed record ReplicaTerminalStateReachedDomainEvent(
    Guid ReplicaId,
    Guid AssetId,
    ReplicaStatus Status) : IDomainEvent;

public sealed class ReplicaTerminalStateReachedDomainEventHandler(IIntegrationEventOutbox outbox)
        : INotificationHandler<ReplicaTerminalStateReachedDomainEvent>
{
    public async Task Handle(ReplicaTerminalStateReachedDomainEvent evt, CancellationToken ct)
    {
        var integrationEvent = new AssetCompletedIntegrationEvent(evt.AssetId, evt.ReplicaId, (int)evt.Status);
        await outbox.Add(integrationEvent);
    }
}
