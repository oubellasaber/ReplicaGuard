using MediatR;
using ReplicaGuard.Contracts.IntegrationEvents;
using ReplicaGuard.Domain.Abstractions;

namespace ReplicaGuard.Domain.Replication.DomainEvents;

public sealed record ReplicaStatusUpdatedDomainEvent(
    Guid ReplicaId, 
    ReplicaStatus Status, 
    DateTime OccurredAt, 
    long? TransferredBytes) : IDomainEvent;

public sealed class ReplicaStatusUpdatedDomainEventHandler(IIntegrationEventOutbox outbox)
        : INotificationHandler<ReplicaStatusUpdatedDomainEvent>
{
    public async Task Handle(ReplicaStatusUpdatedDomainEvent evt, CancellationToken ct)
    {
        var integrationEvent = new ReplicaStatusUpdatedIntegrationEvent(evt.ReplicaId, (int)evt.Status, evt.OccurredAt, evt.TransferredBytes);
        await outbox.Add(integrationEvent);
    }
}
