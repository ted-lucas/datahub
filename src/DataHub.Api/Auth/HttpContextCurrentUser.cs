using System.Security.Claims;
using DataHub.Core.Interfaces;

namespace DataHub.Api.Auth;

/// <summary>
/// ASP.NET Core implementation of <see cref="ICurrentUser"/>. Reads the email claim from the
/// current HttpContext for audit-field stamping. Returns null if there is no HTTP context
/// (background jobs, seeders) — DbContext falls back to "system" in that case.
/// </summary>
public class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextCurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    public string? Identifier =>
        _accessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email)
        ?? _accessor.HttpContext?.User?.FindFirstValue("email");
}
