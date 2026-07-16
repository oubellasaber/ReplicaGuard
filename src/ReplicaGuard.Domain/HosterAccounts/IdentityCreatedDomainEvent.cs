using MediatR;
using ReplicaGuard.Contracts.IntegrationEvents;
using ReplicaGuard.Domain.Abstractions;

namespace ReplicaGuard.Domain.HosterAccounts;

internal sealed record IdentityCreatedDomainEvent(Guid IdentityId) : IDomainEvent;

internal class IdentityCreatedDomainEventHandler(IIntegrationEventOutbox outbox) : INotificationHandler<IdentityCreatedDomainEvent>
{
    public async Task Handle(IdentityCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        var integrationEvent = new IdentityCreatedIntegrationEvent(notification.IdentityId);

        await outbox.Add(integrationEvent);

        return;
    }
}
