using MediatR;
using ReplicaGuard.Contracts.IntegrationEvents;
using ReplicaGuard.Domain.Abstractions;

namespace ReplicaGuard.Domain.Replication.DomainEvents;

public sealed record ReplicaExpiringSoonDomainEvent(
    Guid ReplicaId,
    Guid AssetId,
    Guid HosterId) : IDomainEvent;

public sealed class ReplicaExpiringSoonDomainEventHandler(IIntegrationEventOutbox outbox)
    : INotificationHandler<ReplicaExpiringSoonDomainEvent>
{
    public async Task Handle(ReplicaExpiringSoonDomainEvent evt, CancellationToken ct)
    {
        await outbox.Add(new ReplicaExpiringSoonIntegrationEvent(
            evt.ReplicaId, evt.AssetId, evt.HosterId));
    }
}
