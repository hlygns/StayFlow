using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using StayFlow.Application.Reservations;
using StayFlow.Application.Rooms;
using StayFlow.Infrastructure.Persistence;
using StayFlow.Tests.Api;

namespace StayFlow.Tests.Containers;

/// <summary>
/// Projenin ana iddiasini gercek SQL Server'da kanitlar: ayni oda ayni geceler icin es zamanli
/// istekler geldiginde tam olarak biri kazanir. Istekler gercekten paralel gonderilir.
/// </summary>
[Trait("Category", "Containers")]
public class ConcurrencyOnSqlServerTests : IClassFixture<RealInfrastructureFixture>
{
    private readonly RealInfrastructureFixture _infra;

    public ConcurrencyOnSqlServerTests(RealInfrastructureFixture infra)
    {
        _infra = infra;
    }

    private static async Task<List<RoomDto>> CreateRoomsAsync(HttpClient client, int count)
    {
        var roomType = await (await client.PostAsJsonAsync("/api/room-types", new { name = "Standart", capacity = 2, basePrice = 1500 }))
            .Content.ReadFromJsonAsync<RoomTypeDto>(TestJson.Options);
        var rooms = new List<RoomDto>();
        for (var i = 1; i <= count; i++)
        {
            var response = await client.PostAsJsonAsync("/api/rooms", new { roomNumber = $"{100 + i}", floor = 1, roomTypeId = roomType!.Id });
            rooms.Add((await response.Content.ReadFromJsonAsync<RoomDto>(TestJson.Options))!);
        }
        return rooms;
    }

    [Fact]
    public async Task Ayni_oda_icin_30_es_zamanli_istekten_tam_olarak_biri_kazanir()
    {
        var client = await _infra.RegisterHotelAsync();
        var room = (await CreateRoomsAsync(client, 1))[0];
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(40);

        // Her istek kendi HttpClient'i ile, ayni anda baslatilir
        using var start = new ManualResetEventSlim(false);
        var tasks = Enumerable.Range(0, 30).Select(i => Task.Run(async () =>
        {
            start.Wait();
            return (await client.PostAsJsonAsync("/api/reservations", new
            {
                roomId = room.Id, checkIn, checkOut = checkIn.AddDays(3), guest = new { fullName = $"Misafir {i}" }
            })).StatusCode;
        })).ToList();
        start.Set();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(s => s == HttpStatusCode.Created));
        Assert.Equal(29, results.Count(s => s == HttpStatusCode.Conflict));

        // Veritabaninda: tek rezervasyon, tam 3 gece, kaybedenlerden hic artik kayit yok
        using var scope = _infra.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StayFlowDbContext>();
        Assert.Equal(1, await db.Reservations.IgnoreQueryFilters().CountAsync(r => r.RoomId == room.Id));
        Assert.Equal(3, await db.RoomNights.IgnoreQueryFilters().CountAsync(rn => rn.RoomId == room.Id));
        Assert.Equal(1, await db.Guests.IgnoreQueryFilters().CountAsync(g => g.Reservations.Any(r => r.RoomId == room.Id)));
    }

    [Fact]
    public async Task Farkli_odalara_es_zamanli_rezervasyonlar_birbirini_engellemez()
    {
        // Kilitleme sadece ayni oda+geceyi etkilemeli; farkli odalara paralel satis serbestce gecmeli
        var client = await _infra.RegisterHotelAsync();
        var rooms = await CreateRoomsAsync(client, 10);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(50);

        var results = await Task.WhenAll(rooms.Select(room => client.PostAsJsonAsync("/api/reservations", new
        {
            roomId = room.Id, checkIn, checkOut = checkIn.AddDays(2), guest = new { fullName = "Misafir" }
        })));

        Assert.All(results, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
    }

    [Fact]
    public async Task Demo_verisi_SQL_Serverda_sifirlanip_yeniden_olusturulur()
    {
        // SQL Server'daki "tekrar dene" stratejisi elle acilan transaction'a izin vermez;
        // SQLite testleri bunu yakalayamaz - bu test canlidaki gece isinin gercekten calistigini kanitlar.
        using var scope = _infra.Factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DemoDataSeeder>();
        await seeder.SeedAsync();

        await seeder.ResetAsync();

        var db = scope.ServiceProvider.GetRequiredService<StayFlowDbContext>();
        Assert.Equal(1, await db.Hotels.CountAsync(h => h.Slug == "demo-lara-butik-otel"));
        Assert.Equal(12, await db.Rooms.IgnoreQueryFilters().CountAsync(r => r.Hotel.Slug == "demo-lara-butik-otel"));
    }

    [Fact]
    public async Task Redis_onbellegi_rezervasyondan_sonra_gecersiz_kilinir()
    {
        var client = await _infra.RegisterHotelAsync();
        var room = (await CreateRoomsAsync(client, 1))[0];
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60);
        var url = $"/api/availability?checkIn={checkIn:yyyy-MM-dd}&checkOut={checkIn.AddDays(1):yyyy-MM-dd}";

        Assert.Single((await client.GetFromJsonAsync<List<RoomTypeAvailabilityDto>>(url, TestJson.Options))!);
        Assert.Single((await client.GetFromJsonAsync<List<RoomTypeAvailabilityDto>>(url, TestJson.Options))!); // onbellekten

        (await client.PostAsJsonAsync("/api/reservations", new { roomId = room.Id, checkIn, checkOut = checkIn.AddDays(1), guest = new { fullName = "M" } }))
            .EnsureSuccessStatusCode();

        Assert.Empty((await client.GetFromJsonAsync<List<RoomTypeAvailabilityDto>>(url, TestJson.Options))!);
    }
}
