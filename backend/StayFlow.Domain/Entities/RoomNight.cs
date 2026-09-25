using StayFlow.Domain.Common;

namespace StayFlow.Domain.Entities;

/// <summary>
/// Projenin en onemli tablosu: "su oda, su gece, dolu" demek.
/// (RoomId, Date) uzerindeki unique index (bkz. StayFlowDbContext) sayesinde
/// ayni odanin ayni gecesi iki kere satilamaz - kontrol kod seviyesinde degil,
/// veritabani seviyesinde yapilir.
/// </summary>
public class RoomNight : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HotelId { get; set; }
    public Hotel Hotel { get; set; } = null!;

    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public DateOnly Date { get; set; }

    public Guid ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;
}
