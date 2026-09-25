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

    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
