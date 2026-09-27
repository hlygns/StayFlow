using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StayFlow.Application.Common.Exceptions;
using StayFlow.Application.Reservations;
using StayFlow.Domain.Entities;
using StayFlow.Domain.Enums;
using StayFlow.Tests.Common;

namespace StayFlow.Tests.Reservations;

public class ReservationServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();

    // Testler "bugun"e bagli olmasin diye uzak gelecekte tarihler kullaniyoruz.
    private static readonly DateOnly July10 = new(2030, 7, 10);

    private static CreateReservationRequest Request(Guid roomId, DateOnly checkIn, int nights) => new()
    {
        RoomId = roomId,
        CheckIn = checkIn,
        CheckOut = checkIn.AddDays(nights),
        Guest = new GuestInput { FullName = "Ayse Yilmaz" }
    };

    private ReservationService CreateService(SeededHotel hotel, params IInterceptor[] interceptors) =>
        new(_database.CreateContext(hotel.Tenant, interceptors), TimeProvider.System);

    [Fact]
    public async Task Rezervasyon_her_gece_icin_RoomNight_olusturur_ve_fiyati_hesaplar()
    {
        var hotel = await _database.SeedHotelAsync("Otel A", basePrice: 1500m);

        var reservation = await CreateService(hotel).CreateAsync(Request(hotel.RoomIds[0], July10, 3));

        Assert.Equal(ReservationStatus.Pending, reservation.Status);
        Assert.Equal(4500m, reservation.TotalPrice);
        Assert.NotNull(reservation.PaymentDeadline);

        await using var db = _database.CreateContext(hotel.Tenant);
        var nights = await db.RoomNights.Where(rn => rn.ReservationId == reservation.Id).Select(rn => rn.Date).ToListAsync();
        Assert.Equal([July10, July10.AddDays(1), July10.AddDays(2)], nights.Order());
    }

    [Fact]
    public async Task Cakisan_tarihlere_ikinci_rezervasyon_reddedilir()
    {
        var hotel = await _database.SeedHotelAsync("Otel A");
        await CreateService(hotel).CreateAsync(Request(hotel.RoomIds[0], July10, 3));

        // 11-12 Temmuz, 10-13 araligina tamamen giriyor
        await Assert.ThrowsAsync<ConflictException>(() =>
            CreateService(hotel).CreateAsync(Request(hotel.RoomIds[0], July10.AddDays(1), 1)));
    }

    [Fact]
    public async Task Cikis_gunu_ayni_odaya_yeni_giris_yapilabilir()
    {
        var hotel = await _database.SeedHotelAsync("Otel A");
        await CreateService(hotel).CreateAsync(Request(hotel.RoomIds[0], July10, 3));

        var next = await CreateService(hotel).CreateAsync(Request(hotel.RoomIds[0], July10.AddDays(3), 2));

        Assert.Equal(July10.AddDays(3), next.CheckIn);
    }

    [Fact]
    public async Task Iptal_edilen_rezervasyonun_geceleri_tekrar_satilabilir()
    {
        var hotel = await _database.SeedHotelAsync("Otel A");
        var first = await CreateService(hotel).CreateAsync(Request(hotel.RoomIds[0], July10, 3));

        var cancelled = await CreateService(hotel).CancelAsync(first.Id);
        var second = await CreateService(hotel).CreateAsync(Request(hotel.RoomIds[0], July10, 3));

        Assert.Equal(ReservationStatus.Cancelled, cancelled.Status);
        Assert.Equal(ReservationStatus.Pending, second.Status);
    }

    /// <summary>
    /// Projenin "yildiz" testi. Iki istegin yarisini deterministik olarak canlandiriyoruz:
    /// Istek B on kontrolden GECER (oda o an bos), ama tam SaveChanges'e girerken - araya -
    /// Istek A ayni odayi ayni tarihlere kaydediyor. B'nin on kontrolu artik eskimis bilgiye
    /// dayaniyor; onu durduracak tek sey veritabanindaki (RoomId, Date) unique index.
    /// </summary>
    [Fact]
    public async Task Es_zamanli_iki_istekten_sadece_biri_basarili_olur()
    {
        var hotel = await _database.SeedHotelAsync("Otel A");
        var roomId = hotel.RoomIds[0];

        var raceInterceptor = new BeforeFirstSaveInterceptor(() =>
            CreateService(hotel).CreateAsync(Request(roomId, July10.AddDays(1), 2))); // Istek A

        var requestB = CreateService(hotel, raceInterceptor);

        await Assert.ThrowsAsync<ConflictException>(() => requestB.CreateAsync(Request(roomId, July10, 3)));
        Assert.True(raceInterceptor.Fired, "Yaris senaryosu calismadi - test gecersiz.");

        // Veritabaninda sadece A'nin rezervasyonu ve 2 gecesi var; B'den hicbir satir kalmadi.
        await using var db = _database.CreateContext(hotel.Tenant);
        Assert.Equal(1, await db.Reservations.CountAsync());
        Assert.Equal(2, await db.RoomNights.CountAsync());
        Assert.Equal(0, await db.Guests.CountAsync(g => !db.Reservations.Any(r => r.GuestId == g.Id)));
    }

    [Fact]
    public async Task Oda_tipi_rezervasyonunda_yarisi_kaybeden_bir_sonraki_bos_odayi_alir()
    {
        // Aday odalar [101, 102] olarak secildikten SONRA, kayittan hemen once biri 101'i kapiyor.
        // Ilk deneme unique index'e takilir; ayni istek 102'yi dener ve basarir.
        var hotel = await _database.SeedHotelAsync("Otel A", roomNumbers: ["101", "102"]);
        var race = new BeforeFirstSaveInterceptor(() =>
            CreateService(hotel).CreateAsync(Request(hotel.RoomIds[0], July10, 2)));

        var result = await CreateService(hotel, race).CreateForRoomTypeAsync(
            hotel.RoomTypeId, July10, July10.AddDays(2), 1, new GuestInput { FullName = "Mobil Misafir" });

        Assert.True(race.Fired);
        Assert.Equal("102", result.RoomNumber);
        await using var db = _database.CreateContext(hotel.Tenant);
        Assert.Equal(2, await db.Reservations.CountAsync());
        Assert.Equal(4, await db.RoomNights.CountAsync()); // basarisiz denemeden artik satir yok
    }

    [Fact]
    public async Task Veritabani_ayni_oda_ve_geceyi_iki_kere_kabul_etmez()
    {
        // Uygulama kodunu tamamen atlayip dogrudan tabloya yaziyoruz: kisit gercekten DB'de mi?
        var hotel = await _database.SeedHotelAsync("Otel A");
        await CreateService(hotel).CreateAsync(Request(hotel.RoomIds[0], July10, 1));

        await using var db = _database.CreateContext(hotel.Tenant);
        var reservationId = await db.Reservations.Select(r => r.Id).SingleAsync();
        db.RoomNights.Add(new RoomNight { RoomId = hotel.RoomIds[0], Date = July10, ReservationId = reservationId });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Kapasiteden_fazla_misafir_reddedilir()
    {
        var hotel = await _database.SeedHotelAsync("Otel A", capacity: 2);
        var request = Request(hotel.RoomIds[0], July10, 1);
        request.GuestCount = 3;

        await Assert.ThrowsAsync<ValidationException>(() => CreateService(hotel).CreateAsync(request));
    }

    [Theory]
    [InlineData(0)]   // giris = cikis
    [InlineData(-2)]  // cikis giristen once
    [InlineData(31)]  // azami sureyi asiyor
    public async Task Gecersiz_tarih_araligi_reddedilir(int nights)
    {
        var hotel = await _database.SeedHotelAsync("Otel A");

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService(hotel).CreateAsync(Request(hotel.RoomIds[0], July10, nights)));
    }

    [Fact]
    public async Task Baska_otelin_odasina_rezervasyon_yapilamaz()
    {
        var hotelA = await _database.SeedHotelAsync("Otel A");
        var hotelB = await _database.SeedHotelAsync("Otel B");

        // B oteli adina, A otelinin oda Id'si ile istek: tenant filtresi odayi gizler.
        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateService(hotelB).CreateAsync(Request(hotelA.RoomIds[0], July10, 1)));
    }

    public void Dispose() => _database.Dispose();

    /// <summary>SaveChanges veritabanina yazmadan hemen once, bir kereligine verilen islemi calistirir.</summary>
    private sealed class BeforeFirstSaveInterceptor(Func<Task> action) : SaveChangesInterceptor
    {
        public bool Fired { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Fired)
            {
                Fired = true;
                await action();
            }

            return result;
        }
    }
}
