using StayFlow.Domain.Common;

namespace StayFlow.Domain.Entities;

/// <summary>
/// Rezervasyon yapan misafir. Sisteme giris yapmaz (AppUser'dan farkli) -
/// sadece otel tarafindan tutulan bir kayittir.
/// </summary>
public class Guest : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HotelId { get; set; }
    public Hotel Hotel { get; set; } = null!;

    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? IdentityNumber { get; set; }

    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}
