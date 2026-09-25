using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using StayFlow.Application.Common.Interfaces;
using StayFlow.Application.Common.Models;
using StayFlow.Domain.Entities;

namespace StayFlow.API.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _settings;

    public JwtTokenService(JwtSettings settings)
    {
        _settings = settings;
    }

    public (string Token, DateTime ExpiresAt) GenerateAccessToken(AppUser user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            // Kisa claim adlari ("name", "role") kullaniyoruz; Program.cs'te JwtBearer'a
            // rolleri "role" claim'inden okumasini soyluyoruz (MapInboundClaims = false).
            new Claim(JwtRegisteredClaimNames.Name, user.FullName),
            new Claim("role", user.Role.ToString()),
            new Claim("hotelId", user.HotelId.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddMinutes(_settings.AccessTokenMinutes);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public string GenerateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        // MapInboundClaims = false: varsayilan davranista "sub" gibi kisa claim adlarini
        // uzun XML-tabanli isimlere (ClaimTypes.NameIdentifier) donusturur. Biz token'i
        // ureten tarafla ayni isimleri (JwtRegisteredClaimNames.Sub) okumak istiyoruz.
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _settings.Issuer,
            ValidateAudience = true,
            ValidAudience = _settings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey)),
            ValidateLifetime = false // bilerek kapatildi: suresi dolmus token'dan claim okuyoruz
        };

        try
        {
            var principal = handler.ValidateToken(token, validationParameters, out var securityToken);

            var isValidJwt = securityToken is JwtSecurityToken jwt &&
                jwt.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase);

            return isValidJwt ? principal : null;
        }
        catch
        {
            // imza gecersiz, token bozuk, vb. - hepsi "gecersiz refresh istegi" demek
            return null;
        }
    }
}
