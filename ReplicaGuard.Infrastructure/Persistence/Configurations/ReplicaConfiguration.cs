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

        builder.Property(x => x.State)
            .IsRequired();

        builder.Property(x => x.Link)
            .HasConversion(
                uri => uri != null ? uri.ToString() : null,
                value => value != null ? new Uri(value) : null)
            .HasMaxLength(2048);

        builder.Property(x => x.LastError)
            .HasMaxLength(1000);

        builder.Property(x => x.WaitingForReplicaId);

        builder.Property(x => x.RetryCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(x => x.UpdatedAtUtc)
            .HasDefaultValue(null);

        // Indexes for performance
        builder.HasIndex(x => x.AssetId);
        builder.HasIndex(x => x.HosterId);
        builder.HasIndex(x => x.State);
        builder.HasIndex(x => new { x.AssetId, x.HosterId })
            .IsUnique();
    }
}
