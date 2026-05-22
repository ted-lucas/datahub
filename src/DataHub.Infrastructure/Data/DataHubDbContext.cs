using DataHub.Core.Entities;
using DataHub.Core.Entities.Sports;
using DataHub.Core.Interfaces;
using DataHub.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;

namespace DataHub.Infrastructure.Data;

public class DataHubDbContext : DbContext
{
    private readonly ICurrentUser? _currentUser;

    public DataHubDbContext(DbContextOptions<DataHubDbContext> options) : base(options) { }

    public DataHubDbContext(DbContextOptions<DataHubDbContext> options, ICurrentUser currentUser)
        : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<DataSource> DataSources => Set<DataSource>();
    public DbSet<DataEntry> DataEntries => Set<DataEntry>();

    // Sports module
    public DbSet<Sport> Sports => Set<Sport>();
    public DbSet<SportLevel> SportLevels => Set<SportLevel>();
    public DbSet<League> Leagues => Set<League>();
    public DbSet<Conference> Conferences => Set<Conference>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamSeason> TeamSeasons => Set<TeamSeason>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampAuditFields();
        return base.SaveChanges();
    }

    private void StampAuditFields()
    {
        var actor = _currentUser?.Identifier ?? "system";
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedBy ??= actor;
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedBy ??= actor;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedBy = actor;
                // Do not mutate CreatedAt / CreatedBy on update.
                entry.Property(nameof(AuditableEntity.CreatedAt)).IsModified = false;
                entry.Property(nameof(AuditableEntity.CreatedBy)).IsModified = false;
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(UserConfiguration).Assembly);

        // Apply uniform column constraints to every AuditableEntity-derived type.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(AuditableEntity).IsAssignableFrom(entityType.ClrType)) continue;

            modelBuilder.Entity(entityType.ClrType)
                .Property(nameof(AuditableEntity.CreatedBy)).HasMaxLength(256);
            modelBuilder.Entity(entityType.ClrType)
                .Property(nameof(AuditableEntity.UpdatedBy)).HasMaxLength(256);
            modelBuilder.Entity(entityType.ClrType)
                .Property(nameof(AuditableEntity.Source)).HasMaxLength(256);
        }
    }
}
