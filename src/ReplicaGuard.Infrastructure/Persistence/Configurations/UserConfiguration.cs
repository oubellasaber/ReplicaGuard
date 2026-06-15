using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReplicaGuard.Core.Users;

namespace ReplicaGuard.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.IdentityId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Indexes
        builder.HasIndex(x => x.IdentityId)
            .IsUnique();

        builder.HasIndex(x => x.Email)
            .IsUnique();

        builder.HasIndex(x => x.CreatedAtUtc);
    }
}
