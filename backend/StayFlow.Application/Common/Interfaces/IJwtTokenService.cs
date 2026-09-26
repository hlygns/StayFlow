using StayFlow.Domain.Entities;

namespace StayFlow.Application.Common.Interfaces;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAt) GenerateAccessToken(AppUser user);
    string GenerateRefreshToken();
}
