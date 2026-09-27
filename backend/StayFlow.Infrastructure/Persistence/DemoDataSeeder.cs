using Microsoft.EntityFrameworkCore;
using StayFlow.Application.Common;
using StayFlow.Application.Common.Helpers;
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

    /// <summary>
    /// Gercekci yorum havuzu: farkli diller, puanlar ve tekrar eden konular (kahvalti, temizlik, gurultu,
    /// wifi, konum). Yapay zeka ozeti bu "desenleri" yakalayabilsin diye bilerek boyle secildi.
    /// </summary>
    private static readonly (int Rating, string Comment, string? Reply)[] ReviewPool =
    [
        (5, "Kahvaltı harikaydı, özellikle ev yapımı reçeller. Personel çok ilgili, denize yürüme mesafesi büyük artı.", "Güzel yorumunuz için teşekkür ederiz, reçellerimizi annemiz yapıyor! Tekrar bekleriz."),
        (4, "Oda temiz ve ferahtı. Tek sorun akşamları yan taraftaki barın müziğiydi, gece yarısına kadar ses geldi.", null),
        (5, "Perfect location, 5 minutes to the beach. Breakfast was excellent and the staff spoke good English.", null),
        (3, "Konum iyi ama Wi-Fi odada çok zayıftı, sadece lobide çalışıyordu. Fiyatına göre idare eder.", "Geri bildiriminiz için teşekkürler, Wi-Fi altyapımızı bu ay yeniliyoruz."),
        (5, "Sehr sauberes Zimmer, freundliches Personal und ein tolles Frühstück. Wir kommen wieder!", null),
        (4, "Havuz alanı çok güzel ve temizdi. Otopark ücretsiz olması büyük kolaylık. Kahvaltı çeşidi biraz daha artabilir.", null),
        (2, "Klima gürültülü çalışıyordu ve odada nem kokusu vardı. Resepsiyon ilgilendi ama oda değiştirilemedi.", null),
        (5, "Balayımız için seçtik, bizi odada küçük bir sürprizle karşıladılar. Her şey için teşekkürler!", "Mutluluğunuza ortak olduğumuz için biz teşekkür ederiz, ömür boyu mutluluklar!"),
        (4, "Great value for money. Room was a bit small but very clean. Street noise at night was noticeable.", null),
        (5, "Temizlik kusursuzdu, her gün havlular değişti. Kahvaltıdaki gözleme çok lezzetliydi.", null),
        (3, "Otel güzel ama Wi-Fi sürekli koptu, iş için gelmiştim ve zorlandım. Personel güler yüzlü.", null),
        (4, "Отличное расположение, чистые номера и вкусный завтрак. Персонал очень приветливый.", null),
        (5, "Aile olarak kaldık, çocuklar havuza bayıldı. Aile suiti çok geniş ve kullanışlı.", null),
        (4, "Denize yakın, sessiz bir sokakta. Kahvaltı güzel ama saat 10'da bitiyor, biraz erken.", null),
        (1, "Rezervasyonumda deniz manzaralı yazıyordu ama bahçe manzaralı oda verildi. Çok hayal kırıklığı.", "Yaşadığınız karışıklık için özür dileriz. Bu konuyu ekibimizle konuştuk, iletişime geçmek isteriz."),
        (5, "Lovely boutique hotel with character. The owner gave us great tips for restaurants in Kaleiçi.", null),
    ];

    private readonly StayFlowDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly TimeProvider _timeProvider;
    private readonly IAvailabilityCacheInvalidator? _availabilityCache;

    public DemoDataSeeder(
        StayFlowDbContext db, IPasswordHasher passwordHasher, TimeProvider timeProvider, IAvailabilityCacheInvalidator? availabilityCache = null)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _timeProvider = timeProvider;
        _availabilityCache = availabilityCache;
    }

    /// <summary>
    /// Demo oteli silip bugune gore yeniden olusturur. Canli demoda her gece calisir: ziyaretcilerin
    /// yaptigi degisiklikler temizlenir ve takvim her sabah "bugun" etrafinda dolu gorunur.
    /// </summary>
    public async Task ResetAsync(CancellationToken ct = default)
    {
        var hotelId = await _db.Hotels.Where(h => h.Slug == DemoSlug).Select(h => (Guid?)h.Id).FirstOrDefaultAsync(ct);
        if (hotelId is { } id)
        {
            // Tum yabanci anahtarlar "Restrict": cocuklardan ebeveyne dogru sirayla silinir.
            // ExecuteDelete tek SQL komutu calistirir (kayitlari belleğe yuklemez) ama SaveChanges'i
            // atladigi icin onbellek gecersiz kilma burada elle yapiliyor.
            // SQL Server'da "hata olursa tekrar dene" stratejisi acik: elle acilan transaction'lar bu
            // stratejinin icinde calismak zorunda (yoksa EF hata firlatir). Tekrar denenirse tum blok bastan calisir.
            await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(ct);
                await _db.Reviews.IgnoreQueryFilters().Where(x => x.HotelId == id).ExecuteDeleteAsync(ct);
                await _db.GuestPushTokens.IgnoreQueryFilters().Where(x => x.HotelId == id).ExecuteDeleteAsync(ct);
                await _db.Payments.IgnoreQueryFilters().Where(x => x.HotelId == id).ExecuteDeleteAsync(ct);
                await _db.RoomNights.IgnoreQueryFilters().Where(x => x.HotelId == id).ExecuteDeleteAsync(ct);
                await _db.Reservations.IgnoreQueryFilters().Where(x => x.HotelId == id).ExecuteDeleteAsync(ct);
                await _db.Guests.IgnoreQueryFilters().Where(x => x.HotelId == id).ExecuteDeleteAsync(ct);
                await _db.SeasonalPrices.IgnoreQueryFilters().Where(x => x.HotelId == id).ExecuteDeleteAsync(ct);
                await _db.Rooms.IgnoreQueryFilters().Where(x => x.HotelId == id).ExecuteDeleteAsync(ct);
                await _db.RoomTypes.IgnoreQueryFilters().Where(x => x.HotelId == id).ExecuteDeleteAsync(ct);
                await _db.Users.IgnoreQueryFilters().Where(x => x.HotelId == id).ExecuteDeleteAsync(ct);
                await _db.Hotels.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
                await transaction.CommitAsync(ct);
            });

            if (_availabilityCache is not null)
            {
                await _availabilityCache.InvalidateAsync([id], ct);
            }
        }

        await SeedAsync(ct);
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
            Email = "info@stayflow.app",
            Description = "Lara plajına 5 dakika yürüme mesafesinde, bahçeli ve havuzlu butik otel. Kahvaltı dahil, ücretsiz otopark."
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
            ConfirmationCode = ConfirmationCode.New(),
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

        // Konaklamasi bitmis misafirlerin cogu yorum yazar (demo: yorumlar sayfasi ve yapay zeka ozeti icin)
        if (status == ReservationStatus.CheckedOut && random.NextDouble() < 0.8)
        {
            var (rating, comment, reply) = ReviewPool[random.Next(ReviewPool.Length)];
            // Gercek otellerde puanlar yukselere yigilir: dusuk puan cikarsa bir kez daha cek (dusukler daha seyrek)
            if (rating <= 2)
            {
                (rating, comment, reply) = ReviewPool[random.Next(ReviewPool.Length)];
            }

            // Yorum check-out'tan 0-2 gun sonra yazilmis olsun, ama asla gelecekte degil
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var writtenAt = checkOut.AddDays(random.Next(0, 3)).ToDateTime(new TimeOnly(16, 0), DateTimeKind.Utc);
            if (writtenAt > now)
            {
                writtenAt = now.AddHours(-random.Next(1, 6));
            }
            reservation.Review = new Review
            {
                HotelId = hotelId,
                Rating = rating,
                Comment = comment,
                GuestName = guestName,
                CreatedAt = writtenAt,
                OwnerReply = reply,
                OwnerRepliedAt = reply is null ? null : (writtenAt.AddHours(14) < now ? writtenAt.AddHours(14) : now)
            };
        }

        _db.Reservations.Add(reservation);
    }
}
