using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Infrastructure.Persistence;

public sealed class PublishDomainEventsInterceptor : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            await PublishDomainEventsAsync(eventData.Context, cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static async Task PublishDomainEventsAsync(DbContext context, CancellationToken ct)
    {
        var entities = context.ChangeTracker
            .Entries<IEntity>()
            .Where(e => e.Entity.GetDomainEvents().Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = entities
            .SelectMany(e => e.GetDomainEvents())
            .ToList();

        foreach (var entity in entities)
        {
            entity.ClearDomainEvents();
        }

        // Resolve IPublishEndpoint from the DbContext's service provider (same scope)
        var publishEndpoint = context.GetService<IPublishEndpoint>();

        foreach (var domainEvent in domainEvents)
        {
            await publishEndpoint.Publish(domainEvent, domainEvent.GetType(), ct);
        }
    }
}
