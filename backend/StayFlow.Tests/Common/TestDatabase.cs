using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StayFlow.Application.Common.Interfaces;
using StayFlow.Domain.Entities;
using StayFlow.Infrastructure.Persistence;

namespace StayFlow.Tests.Common;

/// <summary>Testte "su an hangi otel adina istek yapiliyor" bilgisini elle ayarlamak icin.</summary>
public class FakeTenantService : ICurrentTenantService
{
    public Guid? HotelId { get; set; }
}

/// <summary>
/// Her test icin bellekte (in-memory) sifirdan bir SQLite veritabani.
/// EF Core'un InMemory provider'i yerine SQLite kullaniyoruz, cunku InMemory unique index'leri
/// UYGULAMAZ - cifte rezervasyon testimiz tam olarak o kisitin calistigini kanitlamali.
/// Baglanti acik kaldigi surece veritabani yasar; ayni baglantiyla birden fazla DbContext
/// acarak "iki farkli HTTP istegi" durumunu taklit edebiliriz.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var db = CreateContext(new FakeTenantService());
        db.Database.EnsureCreated();
    }

    public StayFlowDbContext CreateContext(ICurrentTenantService tenant, params IInterceptor[] interceptors) =>
        CreateContext(tenant, null, interceptors);

    public StayFlowDbContext CreateContext(ICurrentTenantService tenant, IAvailabilityCacheInvalidator? cache, params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<StayFlowDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(interceptors)
            .Options;

        return new StayFlowDbContext(options, tenant, cache);
    }

    /// <summary>Otel + 1 oda tipi + verilen numaralarda odalar olusturur.</summary>
    public async Task<SeededHotel> SeedHotelAsync(string name, decimal basePrice = 1000m, int capacity = 2, params string[] roomNumbers)
    {
        var hotel = new Hotel { Name = name, Slug = name.ToLowerInvariant().Replace(' ', '-') };
        var tenant = new FakeTenantService { HotelId = hotel.Id };
        await using var db = CreateContext(tenant);

        var roomType = new RoomType { HotelId = hotel.Id, Name = "Standart", Capacity = capacity, BasePrice = basePrice };
        db.Hotels.Add(hotel);
        db.RoomTypes.Add(roomType);

        var rooms = (roomNumbers.Length == 0 ? ["101"] : roomNumbers)
            .Select(n => new Room { HotelId = hotel.Id, RoomTypeId = roomType.Id, RoomNumber = n, Floor = 1 })
            .ToList();
        db.Rooms.AddRange(rooms);

        await db.SaveChangesAsync();
        return new SeededHotel(hotel.Id, tenant, roomType.Id, rooms.Select(r => r.Id).ToList());
    }

    public void Dispose() => _connection.Dispose();
}

public record SeededHotel(Guid HotelId, FakeTenantService Tenant, Guid RoomTypeId, List<Guid> RoomIds);
