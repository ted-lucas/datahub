namespace DataHub.Core.Constants;

/// <summary>
/// Centralized permission strings. Used both for seeding the DB and for [Authorize(Policy = ...)] attributes.
/// </summary>
public static class Permissions
{
    public const string UsersRead = "users:read";
    public const string UsersManage = "users:manage";
    public const string RolesManage = "roles:manage";
    public const string DataRead = "data:read";
    public const string DataWrite = "data:write";
    public const string SourcesManage = "sources:manage";

    public static readonly string[] All =
    {
        UsersRead, UsersManage, RolesManage, DataRead, DataWrite, SourcesManage
    };
}

public static class Roles
{
    public const string Admin = "Admin";
}
