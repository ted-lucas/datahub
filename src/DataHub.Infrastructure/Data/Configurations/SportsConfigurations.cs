using DataHub.Core.Entities.Sports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataHub.Infrastructure.Data.Configurations;

public class SportConfiguration : IEntityTypeConfiguration<Sport>
{
    public void Configure(EntityTypeBuilder<Sport> builder)
    {
        builder.ToTable("Sports");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(100);
        builder.Property(s => s.Slug).IsRequired().HasMaxLength(100);
        builder.Property(s => s.IconRef).HasMaxLength(500);
        builder.HasIndex(s => s.Name).IsUnique();
        builder.HasIndex(s => s.Slug).IsUnique();
    }
}

public class SportLevelConfiguration : IEntityTypeConfiguration<SportLevel>
{
    public void Configure(EntityTypeBuilder<SportLevel> builder)
    {
        builder.ToTable("SportLevels");
        builder.HasKey(sl => sl.Id);
        builder.Property(sl => sl.Name).IsRequired().HasMaxLength(100);
        builder.HasOne(sl => sl.Sport)
            .WithMany(s => s.Levels)
            .HasForeignKey(sl => sl.SportId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(sl => new { sl.SportId, sl.Name }).IsUnique();
    }
}

public class LeagueConfiguration : IEntityTypeConfiguration<League>
{
    public void Configure(EntityTypeBuilder<League> builder)
    {
        builder.ToTable("Leagues");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Name).IsRequired().HasMaxLength(200);
        builder.Property(l => l.Abbreviation).HasMaxLength(20);
        builder.Property(l => l.Country).HasMaxLength(100);
        builder.HasOne(l => l.SportLevel)
            .WithMany(sl => sl.Leagues)
            .HasForeignKey(l => l.SportLevelId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(l => new { l.SportLevelId, l.Name }).IsUnique();
    }
}

public class ConferenceConfiguration : IEntityTypeConfiguration<Conference>
{
    public void Configure(EntityTypeBuilder<Conference> builder)
    {
        builder.ToTable("Conferences");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.HasOne(c => c.League)
            .WithMany(l => l.Conferences)
            .HasForeignKey(c => c.LeagueId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(c => c.ParentConference)
            .WithMany(c => c.ChildConferences)
            .HasForeignKey(c => c.ParentConferenceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.LeagueId, c.Name }).IsUnique();
    }
}

public class VenueConfiguration : IEntityTypeConfiguration<Venue>
{
    public void Configure(EntityTypeBuilder<Venue> builder)
    {
        builder.ToTable("Venues");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Name).IsRequired().HasMaxLength(200);
        builder.Property(v => v.Address).HasMaxLength(500);
        builder.Property(v => v.City).HasMaxLength(100);
        builder.Property(v => v.State).HasMaxLength(100);
        builder.Property(v => v.Country).HasMaxLength(100);
        builder.HasIndex(v => v.Name);
    }
}

public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("Teams");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.City).HasMaxLength(100);
        builder.Property(t => t.State).HasMaxLength(100);
        builder.Property(t => t.Country).HasMaxLength(100);
        builder.Property(t => t.PrimaryColor).HasMaxLength(20);
        builder.Property(t => t.SecondaryColor).HasMaxLength(20);
        builder.Property(t => t.LogoRef).HasMaxLength(500);

        builder.HasOne(t => t.League)
            .WithMany(l => l.Teams)
            .HasForeignKey(t => t.LeagueId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Conference)
            .WithMany(c => c.Teams)
            .HasForeignKey(t => t.ConferenceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(t => t.Venue)
            .WithMany(v => v.Teams)
            .HasForeignKey(t => t.VenueId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(t => new { t.LeagueId, t.Name }).IsUnique();
        builder.HasIndex(t => t.State);
    }
}

public class TeamSeasonConfiguration : IEntityTypeConfiguration<TeamSeason>
{
    public void Configure(EntityTypeBuilder<TeamSeason> builder)
    {
        builder.ToTable("TeamSeasons");
        builder.HasKey(ts => ts.Id);
        builder.Property(ts => ts.Notes).HasMaxLength(2000);

        builder.HasOne(ts => ts.Team)
            .WithMany(t => t.Seasons)
            .HasForeignKey(ts => ts.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ts => ts.League)
            .WithMany()
            .HasForeignKey(ts => ts.LeagueId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ts => ts.Conference)
            .WithMany()
            .HasForeignKey(ts => ts.ConferenceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(ts => new { ts.TeamId, ts.Year }).IsUnique();
        builder.HasIndex(ts => new { ts.EffectiveFrom, ts.EffectiveTo });
    }
}
