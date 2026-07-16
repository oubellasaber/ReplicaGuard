using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReplicaGuard.Domain.HosterAccounts;

namespace ReplicaGuard.Infrastructure.Persistence.Configurations;

public sealed class SecretSetConfiguration : IEntityTypeConfiguration<SecretSet>
{
    public void Configure(EntityTypeBuilder<SecretSet> b)
    {
        b.ToTable("secret_sets");

        b.HasKey(x => x.Id);
        
        b.Property(x => x.Id)
            .ValueGeneratedNever()
            .IsRequired();

        b.HasMany(x => x.Secrets)
            .WithOne()
            .HasForeignKey("SecretSetId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
