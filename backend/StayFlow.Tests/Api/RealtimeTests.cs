using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using StayFlow.API.Realtime;
using StayFlow.Application.Common.Interfaces;
using StayFlow.Application.Rooms;

namespace StayFlow.Tests.Api;

/// <summary>SignalR: olay dogru otelin paneline gider, baska otele gitmez; token'siz baglanti reddedilir.</summary>
public class RealtimeTests : IClassFixture<StayFlowApiFactory>
{
    private readonly StayFlowApiFactory _factory;

    public RealtimeTests(StayFlowApiFactory factory)
    {
        _factory = factory;
    }

    private HubConnection Connect(string? accessToken) =>
        new HubConnectionBuilder()
            .WithUrl($"https://localhost{ReservationsHub.Path}", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling; // TestServer WebSocket'i dogrudan desteklemiyor
                options.AccessTokenProvider = () => Task.FromResult(accessToken);
            })
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()))
            .Build();

    [Fact]
    public async Task Yeni_rezervasyon_sadece_kendi_otelinin_paneline_bildirilir()
    {
        var (hotelA, authA) = await _factory.RegisterHotelAsync();
        var (_, authB) = await _factory.RegisterHotelAsync();

        var receivedByA = new TaskCompletionSource<ReservationChangedMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var receivedByB = new List<ReservationChangedMessage>();

        await using var panelA = Connect(authA.AccessToken);
        await using var panelB = Connect(authB.AccessToken);
        panelA.On<ReservationChangedMessage>(ReservationsHub.ReservationChangedEvent, m => receivedByA.TrySetResult(m));
        panelB.On<ReservationChangedMessage>(ReservationsHub.ReservationChangedEvent, m => receivedByB.Add(m));
        await panelA.StartAsync();
        await panelB.StartAsync();

        // Otel A'da rezervasyon olustur
        var roomType = await (await hotelA.PostAsJsonAsync("/api/room-types", new { name = "Std", capacity = 2, basePrice = 1000 }))
            .Content.ReadFromJsonAsync<RoomTypeDto>(TestJson.Options);
        var room = await (await hotelA.PostAsJsonAsync("/api/rooms", new { roomNumber = "101", floor = 1, roomTypeId = roomType!.Id }))
            .Content.ReadFromJsonAsync<RoomDto>(TestJson.Options);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(20);
        (await hotelA.PostAsJsonAsync("/api/reservations", new { roomId = room!.Id, checkIn, checkOut = checkIn.AddDays(1), guest = new { fullName = "M" } }))
            .EnsureSuccessStatusCode();

        var message = await receivedByA.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(ReservationChange.Created, message.Change);

        await Task.Delay(500); // B'ye yanlislikla gitmis olsaydi bu surede gelirdi
        Assert.Empty(receivedByB);
    }

    [Fact]
    public async Task Bildirim_degisikligi_yapan_sekmenin_kimligini_tasir()
    {
        var (hotel, auth) = await _factory.RegisterHotelAsync();
        var received = new TaskCompletionSource<ReservationChangedMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var panel = Connect(auth.AccessToken);
        panel.On<ReservationChangedMessage>(ReservationsHub.ReservationChangedEvent, m => received.TrySetResult(m));
        await panel.StartAsync();

        // Panel her istege kendi sekme kimligini ekler; o sekme ayni degisiklik icin ikinci bildirim gostermez
        hotel.DefaultRequestHeaders.Add(SignalRReservationNotifier.ClientIdHeader, "sekme-1");
        var roomType = await (await hotel.PostAsJsonAsync("/api/room-types", new { name = "Std", capacity = 2, basePrice = 1000 }))
            .Content.ReadFromJsonAsync<RoomTypeDto>(TestJson.Options);
        var room = await (await hotel.PostAsJsonAsync("/api/rooms", new { roomNumber = "101", floor = 1, roomTypeId = roomType!.Id }))
            .Content.ReadFromJsonAsync<RoomDto>(TestJson.Options);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(20);
        (await hotel.PostAsJsonAsync("/api/reservations", new { roomId = room!.Id, checkIn, checkOut = checkIn.AddDays(1), guest = new { fullName = "M" } }))
            .EnsureSuccessStatusCode();

        var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("sekme-1", message.OriginClientId);
    }

    [Fact]
    public async Task Tokensiz_baglanti_reddedilir()
    {
        await using var anonymous = Connect(null);

        await Assert.ThrowsAnyAsync<Exception>(() => anonymous.StartAsync());
    }

    [Fact]
    public async Task Varsayilan_bildirici_SignalR_ve_push_kanallarini_birlestirir()
    {
        _ = _factory.CreateBrowserLikeClient();
        using var scope = _factory.Services.CreateScope();

        Assert.IsType<StayFlow.Infrastructure.Push.CompositeReservationNotifier>(scope.ServiceProvider.GetRequiredService<IReservationNotifier>());
    }
}
