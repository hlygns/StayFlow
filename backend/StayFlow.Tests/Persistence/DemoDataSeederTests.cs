using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using StayFlow.Domain.Enums;
using StayFlow.Infrastructure.Persistence;
using StayFlow.Infrastructure.Security;
using StayFlow.Tests.Common;

namespace StayFlow.Tests.Persistence;

public class DemoDataSeederTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2030, 7, 15, 9, 0, 0, TimeSpan.Zero));
    private static readonly DateOnly Today = new(2030, 7, 15);

    private async Task SeedAsync()
    {
        await using var db = _database.CreateContext(new FakeTenantService());
        await new DemoDataSeeder(db, new PasswordHasher(), _clock).SeedAsync();
    }

    private async Task<StayFlowDbContext> DemoHotelContextAsync()
    {
        await using var lookup = _database.CreateContext(new FakeTenantService());
        var hotelId = await lookup.Hotels.Select(h => h.Id).SingleAsync();
        return _database.CreateContext(new FakeTenantService { HotelId = hotelId });
    }

    [Fact]
    public async Task Iki_kez_calisinca_veriyi_cogaltmaz()
    {
        await SeedAsync();
        await SeedAsync();

        await using var db = await DemoHotelContextAsync();
        Assert.Equal(12, await db.Rooms.CountAsync());
        Assert.Equal(2, await db.Users.CountAsync());
    }

    [Fact]
    public async Task Uretilen_rezervasyonlar_tutarli()
    {
        await SeedAsync(); // RoomNights unique index'ine takilsaydi burada patlardi

        await using var db = await DemoHotelContextAsync();
        var reservations = await db.Reservations.Include(r => r.RoomNights).ToListAsync();

        Assert.True(reservations.Count > 30, $"Demo icin yeterli rezervasyon yok: {reservations.Count}");
        // Konaklayanlar: dun veya once girdi, bugun ya da sonra cikacak (bugun cikacaklar sabah hala otelde)
        Assert.All(reservations.Where(r => r.Status == ReservationStatus.CheckedIn), r => Assert.True(r.CheckIn < Today && Today <= r.CheckOut));
        Assert.All(reservations.Where(r => r.Status == ReservationStatus.CheckedOut), r => Assert.True(r.CheckOut < Today));
        Assert.All(reservations.Where(r => r.Status == ReservationStatus.Cancelled), r => Assert.Empty(r.RoomNights));
        Assert.All(reservations.Where(r => r.Status != ReservationStatus.Cancelled),
            r => Assert.Equal(r.CheckOut.DayNumber - r.CheckIn.DayNumber, r.RoomNights.Count));
        // Demo'da "bugun" dolu gorunmeli: konaklayan misafirler var
        Assert.Contains(reservations, r => r.Status == ReservationStatus.CheckedIn);
    }

    [Fact]
    public async Task Demo_hesabinin_sifresi_dogrulanir()
    {
        await SeedAsync();

        await using var db = await DemoHotelContextAsync();
        var owner = await db.Users.SingleAsync(u => u.Email == DemoDataSeeder.DemoEmail);
        Assert.True(new PasswordHasher().Verify(DemoDataSeeder.DemoPassword, owner.PasswordHash));
        Assert.Equal(UserRole.Owner, owner.Role);
    }

    [Fact]
    public async Task Sifirlama_ziyaretci_degisikliklerini_siler_ve_veriyi_yeni_gune_gore_uretir()
    {
        await SeedAsync();
        await using (var db = await DemoHotelContextAsync())
        {
            db.Guests.Add(new Domain.Entities.Guest { FullName = "Ziyaretcinin eklediği misafir" });
            await db.SaveChangesAsync();
        }

        _clock.Advance(TimeSpan.FromDays(10)); // 10 gun sonra gece 04:00
        await using (var resetDb = _database.CreateContext(new FakeTenantService()))
        {
            await new DemoDataSeeder(resetDb, new PasswordHasher(), _clock).ResetAsync();
        }

        await using var after = await DemoHotelContextAsync();
        Assert.False(await after.Guests.AnyAsync(g => g.FullName == "Ziyaretcinin eklediği misafir"));
        var newToday = Today.AddDays(10);
        Assert.Contains(await after.Reservations.ToListAsync(), r => r.Status == ReservationStatus.CheckedIn && r.CheckIn < newToday && newToday <= r.CheckOut);
        Assert.Equal(12, await after.Rooms.CountAsync());
    }

    public void Dispose() => _database.Dispose();
}
