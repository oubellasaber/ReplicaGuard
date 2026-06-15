using MediatR;
using ReplicaGuard.Contracts.IntegrationEvents;
using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Core.Replication.DomainEvents;

public sealed record AssetCreatedDomainEvent(
    Guid UserId,
    Guid AssetId,
    IReadOnlyCollection<Guid> ReplicasIds) : IDomainEvent;

public sealed class AssetCreatedDomainEventHandler(IIntegrationEventOutbox outbox)
        : INotificationHandler<AssetCreatedDomainEvent>
{
    public async Task Handle(AssetCreatedDomainEvent evt, CancellationToken ct)
    {
        Console.WriteLine("handling the domain event");
        var integrationEvent = new AssetCreatedIntegrationEvent(
            evt.UserId,
            evt.AssetId,
            evt.ReplicasIds
        );
        await outbox.Add(integrationEvent);
        return;
    }
}
