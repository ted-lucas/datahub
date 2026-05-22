using DataHub.Core.DTOs.Auth;

namespace DataHub.Core.Interfaces;

public interface IAuthService
{
    Task<(LoginResponse Response, string RefreshToken)?> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<(RefreshResponse Response, string NewRefreshToken)?> RefreshAsync(string refreshToken, CancellationToken ct = default);
    Task LogoutAsync(string refreshToken, CancellationToken ct = default);
}
