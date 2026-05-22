using DataHub.Core.DTOs.Auth;
using DataHub.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataHub.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const string RefreshCookieName = "dh_refresh";
    private readonly IAuthService _auth;
    private readonly IWebHostEnvironment _env;

    public AuthController(IAuthService auth, IWebHostEnvironment env)
    {
        _auth = auth;
        _env = env;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _auth.LoginAsync(request, ct);
        if (result is null) return Unauthorized(new { error = "Invalid credentials" });

        SetRefreshCookie(result.Value.RefreshToken);
        return Ok(result.Value.Response);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var token) || string.IsNullOrWhiteSpace(token))
            return Unauthorized(new { error = "No refresh token" });

        var result = await _auth.RefreshAsync(token, ct);
        if (result is null)
        {
            Response.Cookies.Delete(RefreshCookieName);
            return Unauthorized(new { error = "Invalid refresh token" });
        }

        SetRefreshCookie(result.Value.NewRefreshToken);
        return Ok(result.Value.Response);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (Request.Cookies.TryGetValue(RefreshCookieName, out var token) && !string.IsNullOrWhiteSpace(token))
        {
            await _auth.LogoutAsync(token, ct);
        }
        Response.Cookies.Delete(RefreshCookieName);
        return NoContent();
    }

    private void SetRefreshCookie(string token)
    {
        Response.Cookies.Append(RefreshCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = !_env.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth",
            Expires = DateTimeOffset.UtcNow.AddDays(7),
        });
    }
}
