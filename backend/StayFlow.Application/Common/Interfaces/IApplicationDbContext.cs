using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
    DbSet<Review> Reviews { get; }

    /// <summary>Basarisiz bir kayit denemesinden sonra izlenen (eklenmeyi bekleyen) nesneleri temizlemek icin.</summary>
    ChangeTracker ChangeTracker { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
