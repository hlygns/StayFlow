using StayFlow.Domain.Common;

namespace StayFlow.Domain.Entities;

/// <summary>
/// "Standart", "Deniz Manzarali", "Suit" gibi oda kategorileri.
/// Fiziksel odalar (Room) bir RoomType'a baglidir.
/// </summary>
public class RoomType : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HotelId { get; set; }
    public Hotel Hotel { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public decimal BasePrice { get; set; }

    public ICollection<Room> Rooms { get; set; } = new List<Room>();
    public ICollection<SeasonalPrice> SeasonalPrices { get; set; } = new List<SeasonalPrice>();
}
