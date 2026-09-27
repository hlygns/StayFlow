using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StayFlow.Application.Reservations;
using StayFlow.Tests.Common;

namespace StayFlow.Tests.Reservations;

public class AvailabilityCacheTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly AvailabilityCache _cache = new(
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), NullLogger<AvailabilityCache>.Instance);
    private static readonly DateOnly July10 = new(2030, 7, 10);

    private AvailabilityService Availability(SeededHotel hotel) =>
        new(_database.CreateContext(hotel.Tenant, _cache), TimeProvider.System, _cache, hotel.Tenant);

    private ReservationService Reservations(SeededHotel hotel) =>
        new(_database.CreateContext(hotel.Tenant, _cache), TimeProvider.System);

    private static async Task<int> FreeRoomsAsync(AvailabilityService service) =>
        (await service.SearchAsync(July10, July10.AddDays(2), guests: null)).Sum(t => t.AvailableRooms);

    [Fact]
    public async Task Ikinci_ayni_arama_onbellekten_gelir()
    {
        var hotel = await _database.SeedHotelAsync("Otel A", roomNumbers: ["101", "102"]);
        Assert.Equal(2, await FreeRoomsAsync(Availability(hotel)));

        // Veriyi EF'i (ve onbellek gecersiz kilmayi) ATLAYARAK degistir: oda pasife alindi
        await using (var db = _database.CreateContext(hotel.Tenant))
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE Rooms SET IsActive = 0 WHERE RoomNumber = '101'");
        }

        // Onbellekteki eski sonuc donuyor: sorgu veritabanina gitmedi
        Assert.Equal(2, await FreeRoomsAsync(Availability(hotel)));
    }

    [Fact]
    public async Task Rezervasyon_kaydedilince_onbellek_gecersiz_olur()
    {
        var hotel = await _database.SeedHotelAsync("Otel A", roomNumbers: ["101", "102"]);
        Assert.Equal(2, await FreeRoomsAsync(Availability(hotel)));

        await Reservations(hotel).CreateAsync(new CreateReservationRequest
        {
            RoomId = hotel.RoomIds[0], CheckIn = July10, CheckOut = July10.AddDays(1), Guest = new GuestInput { FullName = "M" }
        });

        Assert.Equal(1, await FreeRoomsAsync(Availability(hotel)));
    }

    [Fact]
    public async Task Bir_otelin_degisikligi_diger_otelin_onbellegini_bozmaz()
    {
        var hotelA = await _database.SeedHotelAsync("Otel A", roomNumbers: ["101"]);
        var hotelB = await _database.SeedHotelAsync("Otel B", roomNumbers: ["201", "202"]);
        Assert.Equal(2, await FreeRoomsAsync(Availability(hotelB)));

        await using (var db = _database.CreateContext(hotelB.Tenant))
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE Rooms SET IsActive = 0 WHERE RoomNumber = '201'");
        }
        await Reservations(hotelA).CreateAsync(new CreateReservationRequest
        {
            RoomId = hotelA.RoomIds[0], CheckIn = July10, CheckOut = July10.AddDays(1), Guest = new GuestInput { FullName = "M" }
        });

        // A'nin rezervasyonu sadece A'nin surumunu degistirdi; B hala onbellekten (eski deger) okuyor
        Assert.Equal(2, await FreeRoomsAsync(Availability(hotelB)));
    }

    [Fact]
    public async Task Onbellek_calismazsa_arama_yine_calisir()
    {
        var hotel = await _database.SeedHotelAsync("Otel A", roomNumbers: ["101"]);
        var broken = new AvailabilityCache(new BrokenCache(), NullLogger<AvailabilityCache>.Instance);
        var service = new AvailabilityService(_database.CreateContext(hotel.Tenant), TimeProvider.System, broken, hotel.Tenant);

        Assert.Equal(1, await FreeRoomsAsync(service));
    }

    public void Dispose() => _database.Dispose();

    /// <summary>Redis cokmus gibi davranan onbellek.</summary>
    private sealed class BrokenCache : IDistributedCache
    {
        private static Exception Down() => new InvalidOperationException("Redis baglantisi yok");
        public byte[]? Get(string key) => throw Down();
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw Down();
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw Down();
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw Down();
        public void Refresh(string key) => throw Down();
        public Task RefreshAsync(string key, CancellationToken token = default) => throw Down();
        public void Remove(string key) => throw Down();
        public Task RemoveAsync(string key, CancellationToken token = default) => throw Down();
    }
}
