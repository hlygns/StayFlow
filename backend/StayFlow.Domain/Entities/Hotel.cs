namespace StayFlow.Domain.Entities;

/// <summary>
/// Tenant kokudur: her otel kendi verisinin sahibidir.
/// Dikkat: Hotel, ITenantEntity uygulamaz - cunku Hotel'in kendisi tenant'in ta kendisi,
/// bir HotelId'ye "ait" degil.
/// </summary>
public class Hotel
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>Misafir uygulamasinda gorunen kisa tanitim.</summary>
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AppUser> Users { get; set; } = new List<AppUser>();
    public ICollection<RoomType> RoomTypes { get; set; } = new List<RoomType>();
    public ICollection<Room> Rooms { get; set; } = new List<Room>();
    public ICollection<Guest> Guests { get; set; } = new List<Guest>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}
