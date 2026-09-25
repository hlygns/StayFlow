using StayFlow.Domain.Common;

namespace StayFlow.Domain.Entities;

/// <summary>
/// Belirli bir tarih araligi icin bir oda tipinin gecelik fiyati.
/// Ornek: 1 Haziran - 31 Agustos arasi "Deniz Manzarali" oda 2500 TL/gece.
/// </summary>
public class SeasonalPrice : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HotelId { get; set; }
    public Hotel Hotel { get; set; } = null!;

    public Guid RoomTypeId { get; set; }
    public RoomType RoomType { get; set; } = null!;

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal PricePerNight { get; set; }
}
