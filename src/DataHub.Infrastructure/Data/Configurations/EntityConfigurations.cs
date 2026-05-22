using DataHub.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataHub.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.FirstName).HasMaxLength(100);
        builder.Property(u => u.LastName).HasMaxLength(100);
        builder.HasIndex(u => u.Email).IsUnique();
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).IsRequired().HasMaxLength(100);
        builder.Property(r => r.Description).HasMaxLength(500);
        builder.HasIndex(r => r.Name).IsUnique();
    }
}

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Description).HasMaxLength(500);
        builder.HasIndex(p => p.Name).IsUnique();
    }
}

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles");
        builder.HasKey(x => new { x.UserId, x.RoleId });
        builder.HasOne(x => x.User).WithMany(u => u.UserRoles).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Role).WithMany(r => r.UserRoles).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");
        builder.HasKey(x => new { x.RoleId, x.PermissionId });
        builder.HasOne(x => x.Role).WithMany(r => r.RolePermissions).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Permission).WithMany(p => p.RolePermissions).HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(rt => rt.Id);
        builder.Property(rt => rt.Token).IsRequired().HasMaxLength(512);
        builder.HasIndex(rt => rt.Token).IsUnique();
        builder.HasOne(rt => rt.User).WithMany(u => u.RefreshTokens).HasForeignKey(rt => rt.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Ignore(rt => rt.IsActive);
    }
}

public class DataSourceConfiguration : IEntityTypeConfiguration<DataSource>
{
    public void Configure(EntityTypeBuilder<DataSource> builder)
    {
        builder.ToTable("DataSources");
        builder.HasKey(ds => ds.Id);
        builder.Property(ds => ds.Name).IsRequired().HasMaxLength(200);
        builder.Property(ds => ds.Type).IsRequired().HasMaxLength(100);
        builder.Property(ds => ds.Description).HasMaxLength(1000);
        builder.Property(ds => ds.ConfigJson).HasColumnType("nvarchar(max)");
        builder.HasIndex(ds => ds.Name);
    }
}

public class DataEntryConfiguration : IEntityTypeConfiguration<DataEntry>
{
    public void Configure(EntityTypeBuilder<DataEntry> builder)
    {
        builder.ToTable("DataEntries");
        builder.HasKey(de => de.Id);
        builder.Property(de => de.Category).IsRequired().HasMaxLength(200);
        builder.Property(de => de.Tags).HasMaxLength(1000);
        builder.Property(de => de.PayloadJson).IsRequired().HasColumnType("nvarchar(max)");
        builder.HasIndex(de => de.Category);
        builder.HasIndex(de => de.CreatedAt);
        builder.HasOne(de => de.DataSource).WithMany(ds => ds.DataEntries).HasForeignKey(de => de.DataSourceId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(de => de.CreatedByUser).WithMany().HasForeignKey(de => de.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
