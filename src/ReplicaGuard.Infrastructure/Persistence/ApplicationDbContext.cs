using MassTransit;
using Microsoft.EntityFrameworkCore;
using ReplicaGuard.Core.Abstractions;
using ConcurrencyException = ReplicaGuard.Application.Exceptions.ConcurrencyException;

namespace ReplicaGuard.Infrastructure.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IUnitOfWork
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(Schemas.Application);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // MassTransit Transactional Outbox tables
        builder.AddInboxStateEntity(cfg => cfg.ToTable("inbox_state", Schemas.Transport));
        builder.AddOutboxMessageEntity(cfg => cfg.ToTable("outbox_message", Schemas.Transport));
        builder.AddOutboxStateEntity(cfg => cfg.ToTable("outbox_state", Schemas.Transport));
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("Concurrency exception occurred.", ex);
        }
    }
}
