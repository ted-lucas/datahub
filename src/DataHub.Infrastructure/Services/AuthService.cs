using DataHub.Core.DTOs.Auth;
using DataHub.Core.Entities;
using DataHub.Core.Interfaces;
using DataHub.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DataHub.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly DataHubDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IPasswordHasher<User> _hasher;

    public AuthService(DataHubDbContext db, ITokenService tokens, IPasswordHasher<User> hasher)
    {
        _db = db;
        _tokens = tokens;
        _hasher = hasher;
    }

    public async Task<(LoginResponse Response, string RefreshToken)?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive, ct);

        if (user is null) return null;

        var verifyResult = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verifyResult == PasswordVerificationResult.Failed) return null;

        var roles = user.UserRoles.Select(ur => ur.Role.Name).Distinct().ToList();
        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission.Name))
            .Distinct().ToList();

        var (accessToken, accessExpires) = _tokens.CreateAccessToken(user, roles, permissions);
        var (refreshToken, refreshExpires) = _tokens.CreateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = refreshExpires
        });
        await _db.SaveChangesAsync(ct);

        var userDto = new UserDto(user.Id, user.Email, user.FirstName, user.LastName, user.IsActive, roles, permissions);
        return (new LoginResponse(accessToken, accessExpires, userDto), refreshToken);
    }

    public async Task<(RefreshResponse Response, string NewRefreshToken)?> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var existing = await _db.RefreshTokens
            .Include(rt => rt.User)
                .ThenInclude(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken, ct);

        if (existing is null || existing.RevokedAt != null || existing.ExpiresAt <= DateTime.UtcNow)
            return null;

        if (!existing.User.IsActive) return null;

        // rotate
        existing.RevokedAt = DateTime.UtcNow;

        var roles = existing.User.UserRoles.Select(ur => ur.Role.Name).Distinct().ToList();
        var permissions = existing.User.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission.Name))
            .Distinct().ToList();

        var (accessToken, accessExpires) = _tokens.CreateAccessToken(existing.User, roles, permissions);
        var (newRefresh, newRefreshExpires) = _tokens.CreateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = existing.UserId,
            Token = newRefresh,
            ExpiresAt = newRefreshExpires
        });
        await _db.SaveChangesAsync(ct);

        return (new RefreshResponse(accessToken, accessExpires), newRefresh);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var existing = await _db.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == refreshToken, ct);
        if (existing is { RevokedAt: null })
        {
            existing.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }
}
