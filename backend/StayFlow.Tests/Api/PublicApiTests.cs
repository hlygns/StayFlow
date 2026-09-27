using System.Net;
using System.Net.Http.Json;
using StayFlow.Application.Public;
using StayFlow.Application.Reservations;
using StayFlow.Application.Rooms;
using StayFlow.Domain.Enums;

namespace StayFlow.Tests.Api;

/// <summary>Misafir uygulamasinin uclari: otel arama, rezervasyon, kod+e-posta erisimi, simule odeme, iptal.</summary>
public class PublicApiTests : IClassFixture<StayFlowApiFactory>
{
    private readonly StayFlowApiFactory _factory;
    private static readonly DateOnly CheckIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);

    public PublicApiTests(StayFlowApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Otel sahibi: 1 oda tipi, verilen sayida oda. Donus: panel istemcisi, otel slug'i, oda tipi, odalar.</summary>
    private async Task<(HttpClient Owner, string Slug, RoomTypeDto Type, List<RoomDto> Rooms)> HotelWithRoomsAsync(int roomCount)
    {
        var (owner, _) = await _factory.RegisterHotelAsync();
        var slug = (await owner.GetFromJsonAsync<Application.Hotels.HotelDto>("/api/hotel", TestJson.Options))!.Slug;
        var type = (await (await owner.PostAsJsonAsync("/api/room-types", new { name = "Deniz Manzaralı", capacity = 2, basePrice = 2000 }))
            .Content.ReadFromJsonAsync<RoomTypeDto>(TestJson.Options))!;
        var rooms = new List<RoomDto>();
        for (var i = 1; i <= roomCount; i++)
        {
            rooms.Add((await (await owner.PostAsJsonAsync("/api/rooms", new { roomNumber = $"{200 + i}", floor = 2, roomTypeId = type.Id }))
                .Content.ReadFromJsonAsync<RoomDto>(TestJson.Options))!);
        }
        return (owner, slug, type, rooms);
    }

    private static object Booking(Guid roomTypeId, string email = "ayse@test.com") => new
    {
        roomTypeId, checkIn = CheckIn, checkOut = CheckIn.AddDays(2), guestCount = 2,
        guest = new { fullName = "Ayşe Yılmaz", email, phone = "5551112233" }
    };

    [Fact]
    public async Task Misafir_rezervasyon_yapar_oder_ve_panelde_onayli_gorunur()
    {
        var (owner, slug, type, _) = await HotelWithRoomsAsync(2);
        var guest = _factory.CreateBrowserLikeClient(); // oturumsuz

        // 1) Arama ve musaitlik
        var hotels = await guest.GetFromJsonAsync<List<PublicHotelSummary>>("/api/public/hotels", TestJson.Options);
        Assert.Contains(hotels!, h => h.Slug == slug && h.MinPricePerNight == 2000);
        var availability = await guest.GetFromJsonAsync<List<PublicAvailability>>(
            $"/api/public/hotels/{slug}/availability?checkIn={CheckIn:yyyy-MM-dd}&checkOut={CheckIn.AddDays(2):yyyy-MM-dd}", TestJson.Options);
        Assert.Equal(4000, Assert.Single(availability!).TotalPrice);

        // 2) Rezervasyon: kod ile, "odeme bekliyor"
        var booked = await guest.PostAsJsonAsync($"/api/public/hotels/{slug}/reservations", Booking(type.Id));
        Assert.Equal(HttpStatusCode.Created, booked.StatusCode);
        var reservation = (await booked.Content.ReadFromJsonAsync<GuestReservation>(TestJson.Options))!;
        Assert.Matches("^[A-Z2-9]{8}$", reservation.ConfirmationCode);
        Assert.Equal(ReservationStatus.Pending, reservation.Status);
        Assert.True(reservation.CanPay);

        // 3) Reddedilen test karti
        var declined = await guest.PostAsJsonAsync($"/api/public/reservations/{reservation.ConfirmationCode}/pay",
            new { email = "ayse@test.com", cardHolder = "AYSE YILMAZ", cardNumber = "4000 0000 0000 0002", expiry = "12/30", cvc = "123" });
        Assert.Equal(HttpStatusCode.BadRequest, declined.StatusCode);

        // 4) Basarili odeme -> onaylandi
        var paid = await guest.PostAsJsonAsync($"/api/public/reservations/{reservation.ConfirmationCode.ToLowerInvariant()}/pay",
            new { email = "AYSE@test.com", cardHolder = "AYSE YILMAZ", cardNumber = "4242 4242 4242 4242", expiry = "12/30", cvc = "123" });
        var confirmed = (await paid.Content.ReadFromJsonAsync<GuestReservation>(TestJson.Options))!;
        Assert.Equal(ReservationStatus.Confirmed, confirmed.Status);
        Assert.Equal("Kart •••• 4242 (simülasyon)", confirmed.PaymentMethod);

        // 5) Otel panelinde ayni kodla onayli gorunur
        var panel = await owner.GetFromJsonAsync<List<ReservationDto>>("/api/reservations", TestJson.Options);
        var inPanel = Assert.Single(panel!);
        Assert.Equal(reservation.ConfirmationCode, inPanel.ConfirmationCode);
        Assert.Equal(ReservationStatus.Confirmed, inPanel.Status);

        // 6) Misafir iptal eder
        var cancelled = await guest.PostAsJsonAsync($"/api/public/reservations/{reservation.ConfirmationCode}/cancel", new { email = "ayse@test.com" });
        Assert.Equal(ReservationStatus.Cancelled, (await cancelled.Content.ReadFromJsonAsync<GuestReservation>(TestJson.Options))!.Status);
    }

    [Fact]
    public async Task Zamanlar_UTC_isaretiyle_doner_istemci_yerel_saat_sanmaz()
    {
        // Onceki hata: "2026-09-27T10:15:26" (Z yok) -> telefon/tarayici yerel saat sanip 3 saat kaydiriyordu
        var (_, slug, type, _) = await HotelWithRoomsAsync(1);
        var guest = _factory.CreateBrowserLikeClient();
        var booked = await (await guest.PostAsJsonAsync($"/api/public/hotels/{slug}/reservations", Booking(type.Id))).Content.ReadFromJsonAsync<GuestReservation>(TestJson.Options);

        var json = await (await guest.PostAsJsonAsync($"/api/public/reservations/{booked!.ConfirmationCode}", new { email = "ayse@test.com" })).Content.ReadAsStringAsync();

        Assert.Matches("\"paymentDeadline\":\"[^\"]+Z\"", json);
    }

    [Fact]
    public async Task Kod_dogru_ama_eposta_yanlissa_rezervasyon_gosterilmez()
    {
        var (_, slug, type, _) = await HotelWithRoomsAsync(1);
        var guest = _factory.CreateBrowserLikeClient();
        var reservation = await (await guest.PostAsJsonAsync($"/api/public/hotels/{slug}/reservations", Booking(type.Id)))
            .Content.ReadFromJsonAsync<GuestReservation>(TestJson.Options);

        var wrongEmail = await guest.PostAsJsonAsync($"/api/public/reservations/{reservation!.ConfirmationCode}", new { email = "baskasi@test.com" });
        var wrongCode = await guest.PostAsJsonAsync("/api/public/reservations/AAAAAAAA", new { email = "ayse@test.com" });

        // Ikisi de ayni cevap: hangisinin yanlis oldugu sizmaz
        Assert.Equal(HttpStatusCode.NotFound, wrongEmail.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, wrongCode.StatusCode);
        Assert.Equal(await wrongEmail.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>() is { } a ? a.Detail : null,
                     await wrongCode.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>() is { } b ? b.Detail : null);
    }

    [Fact]
    public async Task Oda_tipi_rezervasyonu_dolu_odayi_atlar_yer_kalmayinca_409_verir()
    {
        var (owner, slug, type, rooms) = await HotelWithRoomsAsync(2);
        // Personel ilk odayi (201) bu tarihlere satti
        (await owner.PostAsJsonAsync("/api/reservations", new { roomId = rooms[0].Id, checkIn = CheckIn, checkOut = CheckIn.AddDays(2), guest = new { fullName = "Resepsiyon Misafiri" } }))
            .EnsureSuccessStatusCode();
        var guest = _factory.CreateBrowserLikeClient();

        var second = await guest.PostAsJsonAsync($"/api/public/hotels/{slug}/reservations", Booking(type.Id));
        var third = await guest.PostAsJsonAsync($"/api/public/hotels/{slug}/reservations", Booking(type.Id, "ucuncu@test.com"));

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, third.StatusCode);
        var panel = await owner.GetFromJsonAsync<List<ReservationDto>>("/api/reservations", TestJson.Options);
        Assert.Equal(["201", "202"], panel!.Select(r => r.RoomNumber).Order());
    }

    [Fact]
    public async Task Misafir_cevaplari_oda_numarasi_ve_ic_Idleri_sizdirmaz()
    {
        var (_, slug, type, _) = await HotelWithRoomsAsync(1);
        var guest = _factory.CreateBrowserLikeClient();

        var availability = await guest.GetStringAsync($"/api/public/hotels/{slug}/availability?checkIn={CheckIn:yyyy-MM-dd}&checkOut={CheckIn.AddDays(1):yyyy-MM-dd}");
        var booking = await (await guest.PostAsJsonAsync($"/api/public/hotels/{slug}/reservations", Booking(type.Id))).Content.ReadAsStringAsync();

        foreach (var json in new[] { availability, booking })
        {
            Assert.DoesNotContain("roomNumber", json);
            Assert.DoesNotContain("roomId", json);
            Assert.DoesNotContain("201", json);
        }
    }

    [Fact]
    public async Task Odasi_olmayan_otel_listelenmez_olmayan_slug_404()
    {
        var (_, _) = await _factory.RegisterHotelAsync(); // odasiz
        var guest = _factory.CreateBrowserLikeClient();

        var hotels = await guest.GetFromJsonAsync<List<PublicHotelSummary>>("/api/public/hotels", TestJson.Options);

        Assert.All(hotels!, h => Assert.NotNull(h.MinPricePerNight));
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync("/api/public/hotels/boyle-bir-otel-yok")).StatusCode);
    }
}
