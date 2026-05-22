using DataHub.Core.DTOs.Auth;
using DataHub.Core.DTOs.Users;
using DataHub.Core.Entities;
using DataHub.Core.Interfaces;
using DataHub.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DataHub.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly DataHubDbContext _db;
    private readonly IPasswordHasher<User> _hasher;

    public UserService(DataHubDbContext db, IPasswordHasher<User> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    public async Task<IEnumerable<UserDto>> GetAllAsync(CancellationToken ct = default)
    {
        var users = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .AsNoTracking().ToListAsync(ct);

        return users.Select(ToDto).ToList();
    }

    public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
        return user is null ? null : ToDto(user);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var user = new User
        {
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            IsActive = true,
        };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);
        _db.Users.Add(user);

        if (request.Roles is not null)
        {
            var roleNames = request.Roles.Distinct().ToList();
            var roles = await _db.Roles.Where(r => roleNames.Contains(r.Name)).ToListAsync(ct);
            foreach (var r in roles)
                _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = r.Id });
        }

        await _db.SaveChangesAsync(ct);
        return (await GetByIdAsync(user.Id, ct))!;
    }

    public async Task<UserDto?> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return null;

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        if (request.Roles is not null)
        {
            _db.UserRoles.RemoveRange(user.UserRoles);
            var roleNames = request.Roles.Distinct().ToList();
            var roles = await _db.Roles.Where(r => roleNames.Contains(r.Name)).ToListAsync(ct);
            foreach (var r in roles)
                _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = r.Id });
        }

        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(user.Id, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync(new object?[] { id }, ct);
        if (user is null) return false;
        _db.Users.Remove(user);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static UserDto ToDto(User user)
    {
        var roles = user.UserRoles.Select(ur => ur.Role.Name).Distinct().ToList();
        var perms = user.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission.Name))
            .Distinct().ToList();
        return new UserDto(user.Id, user.Email, user.FirstName, user.LastName, user.IsActive, roles, perms);
    }
}
