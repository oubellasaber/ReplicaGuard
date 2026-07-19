using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReplicaGuard.Domain.HosterAccounts;
using ReplicaGuard.Domain.Hosters;
using ReplicaGuard.Domain.Replication;

namespace ReplicaGuard.Infrastructure.Persistence.Configurations;

internal sealed class ReplicaConfiguration : IEntityTypeConfiguration<Replica>
{
    public void Configure(EntityTypeBuilder<Replica> builder)
    {
        builder.ToTable("replicas");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever()
            .IsRequired();

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

        builder.Property(x => x.LastRecoveryAttemptAtUtc)
            .IsRequired(false);

        builder.Property(x => x.RecoveryAttemptCount)
            .IsRequired()
            .HasDefaultValue(0);

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

        builder.HasMany(a => a.StatusTransitions)
            .WithOne()
            .HasForeignKey(r => r.ReplicaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.StatusTransitions)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_statusTransitions");

        builder.HasOne<Replica>()
            .WithMany()
            .HasForeignKey(x => x.SourceReplicaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.AvailabilityStatus)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.PredictedExpiryAtUtc)
            .IsRequired(false);

        builder.Property(x => x.LastExpirationCheckAtUtc)
            .IsRequired(false);

        // Indexes
        builder.HasIndex(x => x.AssetId);
        builder.HasIndex(x => x.HosterId);
        builder.HasIndex(x => x.HosterAccountId);
        builder.HasIndex(x => x.SourceReplicaId);
        builder.HasIndex(x => x.AvailabilityStatus);
        builder.HasIndex(x => x.PredictedExpiryAtUtc);
    }
}
