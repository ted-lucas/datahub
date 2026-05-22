using DataHub.Core.Entities;
using DataHub.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;

namespace DataHub.Infrastructure.Data;

public class DataHubDbContext : DbContext
{
    public DataHubDbContext(DbContextOptions<DataHubDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<DataSource> DataSources => Set<DataSource>();
    public DbSet<DataEntry> DataEntries => Set<DataEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(UserConfiguration).Assembly);
    }
}
