using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReplicaGuard.Domain.Hosters;

namespace ReplicaGuard.Infrastructure.Persistence.Configurations;

internal sealed class HosterConfiguration : IEntityTypeConfiguration<Hoster>
{
    public void Configure(EntityTypeBuilder<Hoster> builder)
    {
        builder.ToTable("hosters");

        builder.Property(x => x.Code)
            .IsRequired();

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DisplayName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("now()");
    }
}
