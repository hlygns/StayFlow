using Microsoft.EntityFrameworkCore;
using StayFlow.Domain.Entities;

namespace StayFlow.Application.Common.Interfaces;

/// <summary>
/// Application katmaninin veritabanina bakis acisi. Somut StayFlowDbContext (Infrastructure)
/// bunu uygular. Boylece servis siniflarimiz "EF Core + SQL Server" yerine bu soyut
/// sozlesmeye bagimli olur - test yazarken sahte (fake) bir implementasyon vermek daha kolay olur.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Hotel> Hotels { get; }
    DbSet<AppUser> Users { get; }
    DbSet<RoomType> RoomTypes { get; }
    DbSet<Room> Rooms { get; }
    DbSet<SeasonalPrice> SeasonalPrices { get; }
    DbSet<Guest> Guests { get; }
    DbSet<Reservation> Reservations { get; }
    DbSet<RoomNight> RoomNights { get; }
    DbSet<Payment> Payments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
