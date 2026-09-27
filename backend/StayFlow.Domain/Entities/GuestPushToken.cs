using StayFlow.Domain.Common;

namespace StayFlow.Domain.Entities;

/// <summary>
/// Misafirin telefonundaki Expo push token'i. Misafirin hesabi olmadigi icin token rezervasyona baglanir:
/// "bu rezervasyonda bir degisiklik olursa su telefona haber ver".
/// </summary>
public class GuestPushToken : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HotelId { get; set; }
    public Hotel Hotel { get; set; } = null!;

    public Guid ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;

    public string Token { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
