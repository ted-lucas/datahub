using DataHub.Core.DTOs.Auth;
using DataHub.Core.DTOs.Users;

namespace DataHub.Core.Interfaces;

public interface IUserService
{
    Task<IEnumerable<UserDto>> GetAllAsync(CancellationToken ct = default);
    Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UserDto?> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
