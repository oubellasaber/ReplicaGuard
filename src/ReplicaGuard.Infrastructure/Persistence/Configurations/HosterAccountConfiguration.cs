using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReplicaGuard.Domain.HosterAccounts;
using ReplicaGuard.Domain.Hosters;

namespace ReplicaGuard.Infrastructure.Persistence.Configurations;

public sealed class HosterAccountConfiguration : IEntityTypeConfiguration<HosterAccount>
{
    public void Configure(EntityTypeBuilder<HosterAccount> b)
    {
        b.ToTable("hoster_accounts");

        b.HasKey(x => x.Id);

        b.Property(x => x.HosterCode)
            .HasConversion<short>()
            .IsRequired();

        b.Property(x => x.UserId)
            .IsRequired();

        b.Property(x => x.Alias)
            .HasMaxLength(256)
            .IsRequired();

        b.Property(x => x.Description)
            .HasMaxLength(1024)
            .IsRequired(false);

        b.Property(x => x.CreatedAtUtc)
            .IsRequired();

        b.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        b.HasOne<Hoster>()
            .WithMany()
            .HasForeignKey(x => x.HosterCode)
            .HasPrincipalKey(x => x.Code);

        b.HasMany(x => x.Identities)
            .WithOne()
            .HasForeignKey("HosterAccountId") // shadow FK
            .OnDelete(DeleteBehavior.Cascade);
    }
}
