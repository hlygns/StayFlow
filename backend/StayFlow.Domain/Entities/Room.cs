using StayFlow.Domain.Common;

namespace StayFlow.Domain.Entities;

/// <summary>
/// Fiziksel bir oda: "101 numarali oda", 2. kat, Standart tipinde.
/// </summary>
public class Room : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HotelId { get; set; }
    public Hotel Hotel { get; set; } = null!;

    public Guid RoomTypeId { get; set; }
    public RoomType RoomType { get; set; } = null!;

    public string RoomNumber { get; set; } = string.Empty;
    public int Floor { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<RoomNight> RoomNights { get; set; } = new List<RoomNight>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}
