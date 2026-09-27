using StayFlow.Domain.Common;

namespace StayFlow.Domain.Entities;

/// <summary>
/// Misafir yorumu. Sadece konaklamasi tamamlanmis (check-out yapilmis) bir rezervasyon icin,
/// rezervasyon basina bir kez yazilabilir - sahte yorumu zorlastirir.
/// </summary>
public class Review : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HotelId { get; set; }
    public Hotel Hotel { get; set; } = null!;

    public Guid ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;

    /// <summary>1-5 yildiz</summary>
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string GuestName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Otel sahibi uygunsuz bir yorumu gizleyebilir (silinmez, ortalamaya katilmaz).</summary>
    public bool IsPublished { get; set; } = true;

    public string? OwnerReply { get; set; }
    public DateTime? OwnerRepliedAt { get; set; }
}
