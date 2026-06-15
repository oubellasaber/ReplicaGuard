using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReplicaGuard.Core.HosterAccounts;

namespace ReplicaGuard.Infrastructure.Persistence.Configurations;

public sealed class SecretConfiguration : IEntityTypeConfiguration<Secret>
{
    public void Configure(EntityTypeBuilder<Secret> b)
    {
        b.ToTable("secrets");

        b.HasKey(x => x.Id);

        b.Property(x => x.Type)
            .HasConversion<int>()
            .IsRequired();

        b.Property(x => x.CreatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("now()");

        b.Property(x => x.UpdatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("now()");

        b.Property(x => x.Value)
            .HasConversion(
            v => v.CipherBytes,
            v => new SecretValue(v))
            .HasColumnName("encrypted_secret")
            .IsRequired();
    }
}

