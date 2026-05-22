namespace DataHub.Core.Interfaces;

/// <summary>
/// Abstraction over "who is making the current request" so domain/infrastructure code can
/// stamp audit fields without depending on ASP.NET Core's <c>IHttpContextAccessor</c>.
/// </summary>
public interface ICurrentUser
{
    /// <summary>The current user's identifier for audit purposes — typically email. Null if no user (e.g. background jobs).</summary>
    string? Identifier { get; }
}
