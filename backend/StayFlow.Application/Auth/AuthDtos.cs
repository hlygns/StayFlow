using System.ComponentModel.DataAnnotations;
using StayFlow.Domain.Enums;

namespace StayFlow.Application.Auth;

/// <summary>Yeni bir otelin sisteme kaydolmasi: otel bilgileri + ilk kullanici (otel sahibi).</summary>
public class RegisterHotelRequest
{
    [Required, MaxLength(200)] public string HotelName { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string City { get; set; } = string.Empty;
    [MaxLength(500)] public string Address { get; set; } = string.Empty;
    [MaxLength(30)] public string Phone { get; set; } = string.Empty;
    [EmailAddress] public string? HotelEmail { get; set; }

    [Required, MaxLength(200)] public string OwnerFullName { get; set; } = string.Empty;
    [Required, EmailAddress] public string OwnerEmail { get; set; } = string.Empty;
    [Required, MinLength(8)] public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public record UserDto(Guid Id, string FullName, string Email, UserRole Role, Guid HotelId);

/// <summary>
/// Istemciye donen cevap. Refresh token BURADA YOK: JavaScript'in erisemedigi httpOnly bir cookie
/// olarak gonderilir (bkz. AuthController). Boylece bir XSS acigi refresh token'i calamaz.
/// </summary>
public record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    UserDto User);

/// <summary>Servisten controller'a: cevap + cookie'ye yazilacak refresh token.</summary>
public record AuthResult(AuthResponse Response, string RefreshToken, DateTime RefreshTokenExpiresAt);
