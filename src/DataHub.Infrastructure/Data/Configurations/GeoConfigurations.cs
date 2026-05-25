using DataHub.Core.Entities.Geo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataHub.Infrastructure.Data.Configurations;

public class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> b)
    {
        b.ToTable("Countries");
        b.HasKey(x => x.Id);

        b.Property(x => x.Iso2).IsRequired().HasMaxLength(2);
        b.Property(x => x.Iso3).HasMaxLength(3);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);

        b.HasIndex(x => x.Iso2).IsUnique();
        b.HasIndex(x => x.Name);

        b.HasMany(x => x.States)
            .WithOne(x => x.Country!)
            .HasForeignKey(x => x.CountryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class StateConfiguration : IEntityTypeConfiguration<State>
{
    public void Configure(EntityTypeBuilder<State> b)
    {
        b.ToTable("States");
        b.HasKey(x => x.Id);

        b.Property(x => x.Code).IsRequired().HasMaxLength(10);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.Fips).HasMaxLength(10);

        b.HasIndex(x => new { x.CountryId, x.Code }).IsUnique();
        b.HasIndex(x => x.Fips);

        b.HasMany(x => x.Counties)
            .WithOne(x => x.State!)
            .HasForeignKey(x => x.StateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CountyConfiguration : IEntityTypeConfiguration<County>
{
    public void Configure(EntityTypeBuilder<County> b)
    {
        b.ToTable("Counties");
        b.HasKey(x => x.Id);

        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.Fips).HasMaxLength(10);

        b.HasIndex(x => new { x.StateId, x.Name });
        b.HasIndex(x => x.Fips).IsUnique();
    }
}
