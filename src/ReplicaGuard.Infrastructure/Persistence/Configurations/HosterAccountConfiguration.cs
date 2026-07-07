using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReplicaGuard.Domain.HosterAccounts;

namespace ReplicaGuard.Infrastructure.Persistence.Configurations;

internal sealed class HosterAccountConfiguration
    : IEntityTypeConfiguration<HosterAccount>
{
    public void Configure(EntityTypeBuilder<HosterAccount> b)
    {
        b.ToTable("hoster_accounts");

        b.HasKey(x => x.Id);

        b.Property(x => x.HosterId)
            .IsRequired();

        b.Property(x => x.UserId)
            .IsRequired();

        b.Property(x => x.Alias)
            .HasMaxLength(256)
            .IsRequired();

        b.Property(x => x.Description)
            .HasMaxLength(1024);

        b.Property(x => x.CreatedAtUtc)
            .IsRequired();

        b.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        b.HasOne(x => x.Hoster)
            .WithMany()
            .HasForeignKey(x => x.HosterId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Identities)
            .WithOne()
            .HasForeignKey("HosterAccountId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
