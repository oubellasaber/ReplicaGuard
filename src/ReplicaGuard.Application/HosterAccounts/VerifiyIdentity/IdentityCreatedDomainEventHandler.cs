using MediatR;
using ReplicaGuard.Contracts.IntegrationEvents;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.HosterAccounts;

namespace ReplicaGuard.Application.HosterAccounts.VerifiyIdentity;

internal class IdentityCreatedDomainEventHandler(IIntegrationEventOutbox outbox) : INotificationHandler<IdentityCreatedDomainEvent>
{
    public async Task Handle(IdentityCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        var integrationEvent = new IdentityCreatedIntegrationEvent(notification.IdentityId);

        await outbox.Add(integrationEvent);

        return;
    }
}
