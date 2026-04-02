using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReplicaGuard.Core.Domain.Credentials;

namespace ReplicaGuard.Infrastructure.Persistence.Configurations;

internal sealed class HosterCredentialsConfiguration : IEntityTypeConfiguration<HosterCredentials>
{
    public void Configure(EntityTypeBuilder<HosterCredentials> builder)
    {
        builder.ToTable("hoster_credentials");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.HosterId)
            .IsRequired();

        builder.Property(x => x.PrimaryCredentials)
            .IsRequired();

        builder.Property(x => x.ApiKey)
            .HasMaxLength(512);

        builder.Property(x => x.Email)
            .HasMaxLength(256);

        builder.Property(x => x.Password)
            .HasMaxLength(512);

        builder.Property(x => x.Username)
            .HasMaxLength(256);

        builder.Property(x => x.SyncStatus)
            .IsRequired()
            .HasDefaultValue(CredentialsSyncStatus.Pending);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(x => x.UpdatedAtUtc)
            .HasDefaultValue(null);

        builder.Property(x => x.Version)
            .IsRequired()
            .IsConcurrencyToken()
            .HasDefaultValue(1);

        // Indexes for performance
        builder.HasIndex(x => x.UserId);

        builder.HasIndex(x => x.HosterId);

        builder.HasIndex(x => new { x.UserId, x.HosterId })
            .IsUnique();

        builder.HasIndex(x => x.SyncStatus);

        builder.HasIndex(x => x.CreatedAtUtc);

        builder.HasIndex(x => x.UpdatedAtUtc);
    }
}
