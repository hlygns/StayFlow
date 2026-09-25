using Microsoft.EntityFrameworkCore;
using StayFlow.Domain.Entities;
using StayFlow.Tests.Common;

namespace StayFlow.Tests.MultiTenancy;

public class TenantIsolationTests : IDisposable
{
    private readonly TestDatabase _database = new();

    [Fact]
    public async Task Her_otel_sadece_kendi_verisini_gorur()
    {
        var hotelA = await _database.SeedHotelAsync("Otel A", roomNumbers: ["101", "102"]);
        var hotelB = await _database.SeedHotelAsync("Otel B", roomNumbers: ["201"]);

        await using var dbA = _database.CreateContext(hotelA.Tenant);
        await using var dbB = _database.CreateContext(hotelB.Tenant);

        Assert.Equal(2, await dbA.Rooms.CountAsync());
        Assert.Equal(1, await dbB.Rooms.CountAsync());
        Assert.Null(await dbB.Rooms.FirstOrDefaultAsync(r => r.Id == hotelA.RoomIds[0]));
    }

    [Fact]
    public async Task Tenant_bilgisi_yoksa_hicbir_satir_donmez()
    {
        await _database.SeedHotelAsync("Otel A");

        await using var db = _database.CreateContext(new FakeTenantService { HotelId = null });

        Assert.Empty(await db.Rooms.ToListAsync());
        Assert.Empty(await db.RoomTypes.ToListAsync());
    }

    [Fact]
    public async Task Yeni_kayda_HotelId_otomatik_atanir()
    {
        var hotel = await _database.SeedHotelAsync("Otel A");

        await using (var db = _database.CreateContext(hotel.Tenant))
        {
            db.Guests.Add(new Guest { FullName = "Misafir" }); // HotelId bilerek verilmedi
            await db.SaveChangesAsync();
        }

        await using var verify = _database.CreateContext(hotel.Tenant);
        Assert.Equal(hotel.HotelId, (await verify.Guests.SingleAsync()).HotelId);
    }

    [Fact]
    public async Task Tenant_yokken_HotelId_verilmemis_kayit_kaydedilmez()
    {
        await using var db = _database.CreateContext(new FakeTenantService { HotelId = null });
        db.Guests.Add(new Guest { FullName = "Sahipsiz misafir" });

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    public void Dispose() => _database.Dispose();
}
