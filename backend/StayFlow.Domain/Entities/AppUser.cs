using StayFlow.Domain.Common;
using StayFlow.Domain.Enums;

namespace StayFlow.Domain.Entities;

/// <summary>
/// Sisteme giris yapan kisi: otel sahibi ya da resepsiyon calisani.
/// </summary>
public class AppUser : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HotelId { get; set; }
    public Hotel Hotel { get; set; } = null!;

    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; }

    /// <summary>Gecerli refresh token'in SHA-256 hash'i (token'in kendisi saklanmaz).</summary>
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiresAt { get; set; }

    /// <summary>
    /// Bir onceki refresh token'in hash'i ve ne zaman degistirildigi. Ayni tarayicida iki sekme
    /// ayni anda yenileme yaparsa ikincisi eski token'i gonderir; kisa bir sure bunu da kabul ederiz.
    /// </summary>
    public string? PreviousRefreshToken { get; set; }
    public DateTime? RefreshTokenRotatedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
