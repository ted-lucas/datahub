using DataHub.Core.Entities;

namespace DataHub.Core.Interfaces;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions);
    (string Token, DateTime ExpiresAt) CreateRefreshToken();
}
