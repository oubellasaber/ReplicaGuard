using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ReplicaGuard.Domain.Abstractions;

namespace ReplicaGuard.Infrastructure.Persistence;

public sealed class PublishDomainEventsInterceptor : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken ct = default)
    {
        if (eventData.Context is not null)
            await DispatchDomainEventsAsync(eventData.Context, ct);

        return await base.SavingChangesAsync(eventData, result, ct);
    }

    private static async Task DispatchDomainEventsAsync(DbContext context, CancellationToken ct)
    {
        // 1. Extract domain events from tracked entities
        var entities = context.ChangeTracker
            .Entries<IEntity>()
            .Where(e => e.Entity.GetDomainEvents().Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = entities
            .SelectMany(e => e.GetDomainEvents())
            .ToList();

        // 2. Clear domain events from entities
        entities.ForEach(e => e.ClearDomainEvents());

        if (domainEvents.Count == 0)
            return;

        // 3. Resolve MediatR from the same DI scope as DbContext
        var mediator = context.GetService<IMediator>();

        // 4. Publish domain events IN MEMORY ONLY
        foreach (var domainEvent in domainEvents)
            await mediator.Publish(domainEvent, ct);
    }
}
