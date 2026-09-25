using System.Security.Claims;
using StayFlow.Domain.Entities;

namespace StayFlow.Application.Common.Interfaces;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAt) GenerateAccessToken(AppUser user);
    string GenerateRefreshToken();

    /// <summary>Suresi dolmus bir access token'dan (imzasi hala gecerliyse) claim'leri okur. Refresh akisi icin.</summary>
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
