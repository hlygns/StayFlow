using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StayFlow.Application.Public;
using StayFlow.Application.Rooms;
using StayFlow.Infrastructure.Persistence;
using StayFlow.Infrastructure.Push;

namespace StayFlow.Tests.Api;

/// <summary>Misafirin telefonuna push: Expo'ya giden HTTP istegi sahte bir handler ile yakalanir.</summary>
public class PushNotificationTests : IClassFixture<PushNotificationTests.FactoryWithFakeExpo>
{
    public class FakeExpo : HttpMessageHandler
    {
        public ConcurrentQueue<(string Url, JsonElement Body)> Requests { get; } = new();
        public string NextTicketError { get; set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)).RootElement.Clone();
            Requests.Enqueue((request.RequestUri!.ToString(), body));
            var tickets = body.EnumerateArray().Select(_ => NextTicketError == ""
                ? (object)new { status = "ok", id = Guid.NewGuid() }
                : new { status = "error", details = new { error = NextTicketError } });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { data = tickets }), Encoding.UTF8, "application/json")
            };
        }
    }

    public class FactoryWithFakeExpo : StayFlowApiFactory
    {
        public FakeExpo Expo { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
                services.AddHttpClient(ExpoPushNotifier.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => Expo));
        }
    }

    private readonly FactoryWithFakeExpo _factory;

    public PushNotificationTests(FactoryWithFakeExpo factory) => _factory = factory;

    private async Task<(HttpClient Guest, string Code)> BookAsync(string email)
    {
        var (owner, _) = await _factory.RegisterHotelAsync();
        var slug = (await owner.GetFromJsonAsync<Application.Hotels.HotelDto>("/api/hotel", TestJson.Options))!.Slug;
        var type = await (await owner.PostAsJsonAsync("/api/room-types", new { name = "Std", capacity = 2, basePrice = 1000 }))
            .Content.ReadFromJsonAsync<RoomTypeDto>(TestJson.Options);
        await owner.PostAsJsonAsync("/api/rooms", new { roomNumber = "101", floor = 1, roomTypeId = type!.Id });
        var guest = _factory.CreateBrowserLikeClient();
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(20);
        var booked = await (await guest.PostAsJsonAsync($"/api/public/hotels/{slug}/reservations", new
        {
            roomTypeId = type.Id, checkIn, checkOut = checkIn.AddDays(1), guestCount = 1, guest = new { fullName = "Ayşe", email }
        })).Content.ReadFromJsonAsync<GuestReservation>(TestJson.Options);
        return (guest, booked!.ConfirmationCode);
    }

    private static object Card(string email) => new { email, cardHolder = "A", cardNumber = "4242424242424242", expiry = "12/30", cvc = "123" };

    [Fact]
    public async Task Odeme_sonrasi_onay_bildirimi_misafirin_telefonuna_gider()
    {
        var (guest, code) = await BookAsync("push1@test.com");
        const string token = "ExponentPushToken[abc123XYZ]";

        Assert.Equal(HttpStatusCode.NoContent,
            (await guest.PostAsJsonAsync($"/api/public/reservations/{code}/push-token", new { email = "push1@test.com", token })).StatusCode);
        // ayni token tekrar gonderilirse cift kayit olmaz
        await guest.PostAsJsonAsync($"/api/public/reservations/{code}/push-token", new { email = "push1@test.com", token });

        (await guest.PostAsJsonAsync($"/api/public/reservations/{code}/pay", Card("push1@test.com"))).EnsureSuccessStatusCode();

        var push = _factory.Expo.Requests.Last(r => r.Body[0].GetProperty("to").GetString() == token);
        Assert.Equal("https://exp.host/--/api/v2/push/send", push.Url);
        Assert.Equal(1, push.Body.GetArrayLength());
        Assert.Equal("Rezervasyonunuz onaylandı", push.Body[0].GetProperty("title").GetString());
        Assert.Equal(code, push.Body[0].GetProperty("data").GetProperty("confirmationCode").GetString());
    }

    [Fact]
    public async Task Uygulamasi_silinmis_cihazin_tokeni_temizlenir()
    {
        var (guest, code) = await BookAsync("push2@test.com");
        await guest.PostAsJsonAsync($"/api/public/reservations/{code}/push-token", new { email = "push2@test.com", token = "ExponentPushToken[deadDevice1]" });

        _factory.Expo.NextTicketError = "DeviceNotRegistered";
        try
        {
            (await guest.PostAsJsonAsync($"/api/public/reservations/{code}/pay", Card("push2@test.com"))).EnsureSuccessStatusCode();
        }
        finally
        {
            _factory.Expo.NextTicketError = "";
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StayFlowDbContext>();
        Assert.False(await db.GuestPushTokens.IgnoreQueryFilters().AnyAsync(t => t.Token == "ExponentPushToken[deadDevice1]"));
    }

    [Theory]
    [InlineData("gecersiz-token")]
    [InlineData("ExponentPushToken[<script>]")]
    public async Task Gecersiz_token_reddedilir(string token)
    {
        var (guest, code) = await BookAsync($"push-{Guid.NewGuid():N}@test.com");

        var response = await guest.PostAsJsonAsync($"/api/public/reservations/{code}/push-token", new { email = "x@test.com", token });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
