using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReplicaGuard.Core.HosterAccounts;

namespace ReplicaGuard.Infrastructure.Persistence.Configurations;

public sealed class AuthIdentityConfiguration : IEntityTypeConfiguration<AuthIdentity>
{
    public void Configure(EntityTypeBuilder<AuthIdentity> b)
    {
        b.ToTable("auth_identities");

        b.HasKey(x => x.Id);

        b.Property(x => x.Type)
            .HasConversion<int>()
            .IsRequired();

        b.Property(x => x.Value)
            .HasMaxLength(512)
            .IsRequired(false);

        b.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        b.Property(x => x.CreatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("now()");

        b.Property(x => x.UpdatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("now()");

        b.HasOne(x => x.SecretSet)
            .WithMany()
            .HasForeignKey("SecretSetId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
