using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReplicaGuard.Domain.Replication;

namespace ReplicaGuard.Infrastructure.Persistence.Configurations;

internal class ReplicaStatusTransitionConfiguration : IEntityTypeConfiguration<ReplicaStatusTransition>
{
    public void Configure(EntityTypeBuilder<ReplicaStatusTransition> builder)
    {
        builder.ToTable("replica_status_transitions");

        builder.HasKey(r => r.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(r => r.ReplicaId)
            .IsRequired();

        builder.Property(r => r.Status)
            .IsRequired();

        builder.Property(r => r.OccurredAt)
            .IsRequired();
    }
}
