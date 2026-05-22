namespace DataHub.Core.DTOs.Auth;

public record LoginRequest(string Email, string Password);

public record LoginResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    UserDto User
);

public record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    IEnumerable<string> Roles,
    IEnumerable<string> Permissions
);

public record RefreshResponse(string AccessToken, DateTime AccessTokenExpiresAt);
