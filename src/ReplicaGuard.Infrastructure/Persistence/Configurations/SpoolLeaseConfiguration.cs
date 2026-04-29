using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReplicaGuard.Infrastructure.Spool;

namespace ReplicaGuard.Infrastructure.Persistence.Configurations;

public class SpoolLeaseConfiguration : IEntityTypeConfiguration<SpoolLease>
{
    public void Configure(EntityTypeBuilder<SpoolLease> builder)
    {
        builder.ToTable("spool_leases");

        builder.HasKey(x => x.AssetId);

        builder.Property(x => x.Version)
            .IsRequired()
            .IsConcurrencyToken()
            .HasDefaultValue(1);
    }
}
