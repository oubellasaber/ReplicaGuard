using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReplicaGuard.Core.Domain.Replication;

namespace ReplicaGuard.Infrastructure.Persistence.Configurations;

internal sealed class ReplicaConfiguration : IEntityTypeConfiguration<Replica>
{
    public void Configure(EntityTypeBuilder<Replica> builder)
    {
        builder.ToTable("replicas");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.AssetId)
            .IsRequired();

        builder.Property(x => x.HosterId)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.Link)
            .HasConversion(
                uri => uri != null ? uri.ToString() : null,
                value => value != null ? new Uri(value) : null)
            .HasMaxLength(2048);

        builder.Property(x => x.WaitingForReplicaId);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP AT TIME ZONE 'UTC'");

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        // Indexes for performance
        builder.HasIndex(x => x.AssetId);
        builder.HasIndex(x => x.HosterId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => new { x.AssetId, x.HosterId })
            .IsUnique();
    }
}
