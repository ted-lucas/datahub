using DataHub.Core.Constants;
using DataHub.Core.Entities;
using DataHub.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DataHub.Infrastructure.Seeding;

/// <summary>
/// Idempotent seeder for permissions, the Admin role, and the initial admin user.
/// Safe to run on every startup.
/// </summary>
public static class DbSeeder
{
    public const string DefaultAdminEmail = "tedlucas@outlook.com";
    public const string DefaultAdminPassword = "DataZilla.247";
    private const string SourceTag = "seed:bootstrap";

    public static async Task SeedAsync(DataHubDbContext db, IPasswordHasher<User> hasher, CancellationToken ct = default)
    {
        // Permissions
        var existingPermNames = await db.Permissions.Select(p => p.Name).ToListAsync(ct);
        foreach (var name in Permissions.All)
        {
            if (!existingPermNames.Contains(name))
                db.Permissions.Add(new Permission { Name = name, Description = name, Source = SourceTag });
        }
        await db.SaveChangesAsync(ct);

        // Admin role
        var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == Roles.Admin, ct);
        if (adminRole is null)
        {
            adminRole = new Role { Name = Roles.Admin, Description = "Full access to all functionality.", Source = SourceTag };
            db.Roles.Add(adminRole);
            await db.SaveChangesAsync(ct);
        }

        // Assign all permissions to admin role
        var allPerms = await db.Permissions.ToListAsync(ct);
        var existingRolePerms = await db.RolePermissions
            .Where(rp => rp.RoleId == adminRole.Id)
            .Select(rp => rp.PermissionId)
            .ToListAsync(ct);
        foreach (var p in allPerms)
        {
            if (!existingRolePerms.Contains(p.Id))
                db.RolePermissions.Add(new RolePermission { RoleId = adminRole.Id, PermissionId = p.Id });
        }
        await db.SaveChangesAsync(ct);

        // Admin user
        var admin = await db.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.Email == DefaultAdminEmail, ct);
        if (admin is null)
        {
            admin = new User
            {
                Email = DefaultAdminEmail,
                FirstName = "Ted",
                LastName = "Lucas",
                IsActive = true,
                Source = SourceTag,
            };
            admin.PasswordHash = hasher.HashPassword(admin, DefaultAdminPassword);
            db.Users.Add(admin);
            await db.SaveChangesAsync(ct);
        }

        if (!await db.UserRoles.AnyAsync(ur => ur.UserId == admin.Id && ur.RoleId == adminRole.Id, ct))
        {
            db.UserRoles.Add(new UserRole { UserId = admin.Id, RoleId = adminRole.Id });
            await db.SaveChangesAsync(ct);
        }

        // Sports module (Baseball / MLB / 30 teams)
        await SportsSeeder.SeedAsync(db, ct);
    }
}
