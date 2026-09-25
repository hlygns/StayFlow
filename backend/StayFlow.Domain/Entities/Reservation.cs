using StayFlow.Domain.Common;
using StayFlow.Domain.Enums;

namespace StayFlow.Domain.Entities;

/// <summary>
/// Bir misafirin bir odayi belirli tarihler icin ayirtmasi.
/// Gercek "dolu/bos" bilgisi burada degil, RoomNights tablosunda tutulur -
/// cifte rezervasyonu engelleyen kisit orada.
/// </summary>
public class Reservation : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HotelId { get; set; }
    public Hotel Hotel { get; set; } = null!;

    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public Guid GuestId { get; set; }
    public Guest Guest { get; set; } = null!;

    public DateOnly CheckIn { get; set; }
    public DateOnly CheckOut { get; set; }
    public int GuestCount { get; set; } = 1;
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
    public decimal TotalPrice { get; set; }

    /// <summary>Bu tarihe kadar odeme yapilmazsa Hangfire rezervasyonu otomatik iptal eder (Faz 2).</summary>
    public DateTime? PaymentDeadline { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<RoomNight> RoomNights { get; set; } = new List<RoomNight>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
