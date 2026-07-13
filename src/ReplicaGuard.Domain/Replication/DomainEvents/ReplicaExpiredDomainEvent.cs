using MediatR;
using ReplicaGuard.Contracts.IntegrationEvents;
using ReplicaGuard.Domain.Abstractions;

namespace ReplicaGuard.Domain.Replication.DomainEvents;

public sealed record ReplicaExpiredDomainEvent(
    Guid ReplicaId,
    Guid AssetId,
    Guid HosterId) : IDomainEvent;

public sealed class ReplicaExpiredDomainEventHandler(IIntegrationEventOutbox outbox)
    : INotificationHandler<ReplicaExpiredDomainEvent>
{
    public async Task Handle(ReplicaExpiredDomainEvent evt, CancellationToken ct)
    {
        await outbox.Add(new ReplicaExpiredIntegrationEvent(
            evt.ReplicaId, evt.AssetId, evt.HosterId));
    }
}
