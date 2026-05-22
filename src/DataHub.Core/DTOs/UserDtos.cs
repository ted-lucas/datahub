namespace DataHub.Core.DTOs.Users;

public record CreateUserRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    IEnumerable<string>? Roles
);

public record UpdateUserRequest(
    string FirstName,
    string LastName,
    bool IsActive,
    IEnumerable<string>? Roles
);
