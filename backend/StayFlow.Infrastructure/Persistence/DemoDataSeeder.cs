using Microsoft.EntityFrameworkCore;
using StayFlow.Application.Common;
using StayFlow.Application.Common.Interfaces;
using StayFlow.Application.Reservations;
using StayFlow.Domain.Entities;
using StayFlow.Domain.Enums;

namespace StayFlow.Infrastructure.Persistence;

/// <summary>
/// Canli demo icin dolu bir otel: oda tipleri, sezon fiyati, 12 oda, ~2 aylik rezervasyon gecmisi/gelecegi.
/// Tarihler "bugun"e gore uretilir - demo her gun acildiginda takvim canli gorunur.
/// Idempotent: demo otel zaten varsa hicbir sey yapmaz. Rastgelelik sabit tohumla (her kurulumda ayni veri).
/// </summary>
public class DemoDataSeeder
{
    public const string DemoEmail = "demo@stayflow.app";
    public const string DemoPassword = "Demo1234!";
    public const string ReceptionEmail = "resepsiyon@stayflow.app";
    private const string DemoSlug = "demo-lara-butik-otel";

    private static readonly string[] GuestNames =
    [
        "Ayşe Yılmaz", "Mehmet Demir", "Anna Schmidt", "Ivan Petrov", "Zeynep Kaya", "John Smith", "Elif Aydın",
        "Hans Müller", "Olga Ivanova", "Can Öztürk", "Sophie Martin", "Mustafa Şahin", "Lena Weber", "Dmitri Sokolov",
        "Emma Johnson", "Burak Çelik", "Katarina Novak", "Selin Arslan", "Thomas Becker", "Natalia Morozova",
        "Oliver Brown", "Deniz Koç", "Marta Kowalska", "Ahmet Yıldız", "Julia Fischer", "Sergey Volkov"
    ];

    private readonly StayFlowDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly TimeProvider _timeProvider;

    public DemoDataSeeder(StayFlowDbContext db, IPasswordHasher passwordHasher, TimeProvider timeProvider)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _timeProvider = timeProvider;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await _db.Hotels.AnyAsync(h => h.Slug == DemoSlug, ct))
        {
            return;
        }

        var random = new Random(42);
        var today = _timeProvider.Today();

        // Istek (HTTP) yok -> tenant bilgisi yok; bu yuzden her kayda HotelId'yi elle veriyoruz.
        var hotel = new Hotel
        {
            Name = "Lara Butik Otel (Demo)",
            Slug = DemoSlug,
            City = "Antalya",
            Address = "Lara Cad. No: 12, Muratpaşa",
            Phone = "0242 000 00 00",
            Email = "info@stayflow.app"
        };
        _db.Hotels.Add(hotel);

        _db.Users.AddRange(
            new AppUser { HotelId = hotel.Id, Email = DemoEmail, FullName = "Demo Otel Sahibi", Role = UserRole.Owner, PasswordHash = _passwordHasher.Hash(DemoPassword) },
            new AppUser { HotelId = hotel.Id, Email = ReceptionEmail, FullName = "Selin Resepsiyon", Role = UserRole.Reception, PasswordHash = _passwordHasher.Hash(DemoPassword) });

        var standard = new RoomType { HotelId = hotel.Id, Name = "Standart", Description = "Bahçe manzaralı, çift kişilik", Capacity = 2, BasePrice = 1800 };
        var seaView = new RoomType { HotelId = hotel.Id, Name = "Deniz Manzaralı", Description = "Balkonlu, deniz manzaralı", Capacity = 3, BasePrice = 2600 };
        var family = new RoomType { HotelId = hotel.Id, Name = "Aile Suiti", Description = "İki yatak odalı, 5 kişilik", Capacity = 5, BasePrice = 4200 };
        _db.RoomTypes.AddRange(standard, seaView, family);

        // Onumuzdeki 2-5. haftalar "yuksek sezon": fiyat hesaplamasi takvimde ve musaitlikte gorulsun
        var highSeason = new SeasonalPrice
        {
            HotelId = hotel.Id, RoomTypeId = seaView.Id, StartDate = today.AddDays(14), EndDate = today.AddDays(35), PricePerNight = 3400
        };
        _db.SeasonalPrices.Add(highSeason);

        var rooms = new List<Room>();
        foreach (var (number, type) in new[]
        {
            ("101", standard), ("102", standard), ("103", standard), ("104", standard), ("105", standard),
            ("201", seaView), ("202", seaView), ("203", seaView), ("204", seaView),
            ("301", family), ("302", family), ("303", seaView)
        })
        {
            rooms.Add(new Room { HotelId = hotel.Id, RoomTypeId = type.Id, RoomType = type, RoomNumber = number, Floor = number[0] - '0' });
        }
        _db.Rooms.AddRange(rooms);

        // Her oda icin 3 hafta oncesinden 5 hafta sonrasina kadar, aralarinda bosluk olan ardisik konaklamalar
        var guestIndex = 0;
        foreach (var room in rooms)
        {
            var cursor = today.AddDays(-21 + random.Next(0, 4));
            while (cursor < today.AddDays(35))
            {
                var nights = random.Next(2, 8);
                var checkIn = cursor;
                var checkOut = checkIn.AddDays(nights);
                cursor = checkOut.AddDays(random.Next(0, 4)); // bazen ayni gun yeni misafir, bazen bos gunler

                // Doluluk %100 gorunmesin: konaklamalarin bir kismini atla
                if (random.NextDouble() < 0.25)
                {
                    continue;
                }

                // Bugun cikacak misafir sabah hala otelde: check-out'u resepsiyon gun icinde yapar
                var status = checkOut < today ? ReservationStatus.CheckedOut
                    : checkIn < today ? ReservationStatus.CheckedIn
                    : checkIn == today ? ReservationStatus.Confirmed // bugun gelecekler: check-in bekliyor
                    : random.NextDouble() < 0.3 ? ReservationStatus.Pending : ReservationStatus.Confirmed;

                AddReservation(hotel.Id, room, checkIn, checkOut, status, GuestNames[guestIndex++ % GuestNames.Length], random, highSeason);
            }
        }

        // Birkac iptal: gecmiste iz birakir ama geceleri doldurmaz
        for (var i = 0; i < 3; i++)
        {
            AddReservation(hotel.Id, rooms[i], today.AddDays(40 + i * 3), today.AddDays(42 + i * 3), ReservationStatus.Cancelled,
                GuestNames[guestIndex++ % GuestNames.Length], random, highSeason);
        }

        await _db.SaveChangesAsync(ct);
    }

    private void AddReservation(Guid hotelId, Room room, DateOnly checkIn, DateOnly checkOut, ReservationStatus status,
        string guestName, Random random, SeasonalPrice highSeason)
    {
        var guest = new Guest
        {
            HotelId = hotelId,
            FullName = guestName,
            Email = $"{guestName.Split(' ')[0].ToLowerInvariant()}{random.Next(10, 99)}@ornek.com",
            Phone = $"05{random.Next(30, 56)}{random.Next(1000000, 9999999)}"
        };

        var prices = room.RoomTypeId == highSeason.RoomTypeId ? [highSeason] : Array.Empty<SeasonalPrice>();
        var reservation = new Reservation
        {
            HotelId = hotelId,
            RoomId = room.Id,
            Guest = guest,
            CheckIn = checkIn,
            CheckOut = checkOut,
            GuestCount = random.Next(1, room.RoomType.Capacity + 1),
            Status = status,
            TotalPrice = PriceCalculator.CalculateTotal(room.RoomType.BasePrice, prices, checkIn, checkOut),
            CreatedAt = checkIn.AddDays(-random.Next(3, 40)).ToDateTime(new TimeOnly(10, 0), DateTimeKind.Utc)
        };

        // Iptal edilenler disinda her gece RoomNights'a yazilir - normal rezervasyon akisiyla ayni kural
        if (status != ReservationStatus.Cancelled)
        {
            foreach (var night in PriceCalculator.Nights(checkIn, checkOut))
            {
                reservation.RoomNights.Add(new RoomNight { HotelId = hotelId, RoomId = room.Id, Date = night });
            }
        }

        _db.Reservations.Add(reservation);
    }
}
