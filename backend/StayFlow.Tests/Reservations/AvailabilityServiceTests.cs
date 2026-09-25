using StayFlow.Application.Reservations;
using StayFlow.Tests.Common;

namespace StayFlow.Tests.Reservations;

public class AvailabilityServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private static readonly DateOnly July10 = new(2030, 7, 10);

    [Fact]
    public async Task Dolu_oda_musaitlikte_gorunmez_bos_oda_gorunur()
    {
        var hotel = await _database.SeedHotelAsync("Otel A", roomNumbers: ["101", "102"]);
        await new ReservationService(_database.CreateContext(hotel.Tenant), TimeProvider.System).CreateAsync(new()
        {
            RoomId = hotel.RoomIds[0],
            CheckIn = July10,
            CheckOut = July10.AddDays(3),
            Guest = new GuestInput { FullName = "Misafir" }
        });

        var service = new AvailabilityService(_database.CreateContext(hotel.Tenant), TimeProvider.System);
        var overlapping = await service.SearchAsync(July10.AddDays(2), July10.AddDays(4), guests: null);
        var afterCheckout = await service.SearchAsync(July10.AddDays(3), July10.AddDays(4), guests: null);

        var room = Assert.Single(Assert.Single(overlapping).Rooms);
        Assert.Equal("102", room.RoomNumber);
        Assert.Equal(2, Assert.Single(afterCheckout).AvailableRooms);
    }

    [Fact]
    public async Task Kapasitesi_yetmeyen_oda_tipi_listelenmez()
    {
        var hotel = await _database.SeedHotelAsync("Otel A", capacity: 2);
        var service = new AvailabilityService(_database.CreateContext(hotel.Tenant), TimeProvider.System);

        Assert.Empty(await service.SearchAsync(July10, July10.AddDays(1), guests: 3));
        Assert.Single(await service.SearchAsync(July10, July10.AddDays(1), guests: 2));
    }

    [Fact]
    public async Task Otel_sadece_kendi_odalarini_gorur()
    {
        await _database.SeedHotelAsync("Otel A", roomNumbers: ["101", "102", "103"]);
        var hotelB = await _database.SeedHotelAsync("Otel B", roomNumbers: ["1"]);

        var result = await new AvailabilityService(_database.CreateContext(hotelB.Tenant), TimeProvider.System)
            .SearchAsync(July10, July10.AddDays(1), guests: null);

        Assert.Equal(1, Assert.Single(result).AvailableRooms);
    }

    public void Dispose() => _database.Dispose();
}
