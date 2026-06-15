using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReplicaGuard.Core.HosterAccounts;
using ReplicaGuard.Core.Hosters;
using ReplicaGuard.Core.Replication;

namespace ReplicaGuard.Infrastructure.Persistence.Configurations;

internal sealed class ReplicaConfiguration : IEntityTypeConfiguration<Replica>
{
    public void Configure(EntityTypeBuilder<Replica> builder)
    {
        builder.ToTable("replicas");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .HasConversion<int>()
           .IsRequired();

        builder.Property(x => x.Link)
            .HasConversion(
                uri => uri != null ? uri.ToString() : null,
                value => value != null ? new Uri(value) : null)
            .HasMaxLength(2048);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.HasOne<Asset>()
            .WithMany(a => a.Replicas)
            .HasForeignKey(x => x.AssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Hoster>()
            .WithMany()
            .HasForeignKey(x => x.HosterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<HosterAccount>()
            .WithMany()
            .HasForeignKey(x => x.HosterAccountId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Replica>()
            .WithMany()
            .HasForeignKey(x => x.WaitingForReplicaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(x => x.AssetId);
        builder.HasIndex(x => x.HosterId);
        builder.HasIndex(x => new { x.AssetId, x.Id, x.HosterId, x.HosterAccountId }).IsUnique();
    }
}
