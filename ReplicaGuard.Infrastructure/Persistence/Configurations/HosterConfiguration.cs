using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReplicaGuard.Core.Domain.Hoster;

namespace ReplicaGuard.Infrastructure.Persistence.Configurations;

internal sealed class HosterConfiguration : IEntityTypeConfiguration<Hoster>
{
    public void Configure(EntityTypeBuilder<Hoster> builder)
    {
        builder.ToTable("hosters");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.DisplayName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.PrimaryCredentials)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(x => x.UpdatedAtUtc)
            .HasDefaultValue(null);

        // Indexes for performance
        builder.HasIndex(x => x.Code)
            .IsUnique();

        builder.HasIndex(x => x.CreatedAtUtc);

        // Configure owned collection for FeatureRequirements
        builder.OwnsMany(x => x.Requirements, req =>
        {
            req.ToTable("hoster_feature_requirements");

            req.WithOwner()
                .HasForeignKey("hoster_id");

            req.Property<int>("Id")
                .ValueGeneratedOnAdd();

            req.HasKey("Id");

            req.Property(r => r.Feature)
                .IsRequired();

            req.Property(r => r.RequiredAuth)
                .IsRequired();

            req.HasIndex("hoster_id");
        });

        builder.Navigation(x => x.Requirements)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
