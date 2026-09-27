using System.Net;
using System.Net.Http.Json;
using StayFlow.Application.Public;
using StayFlow.Application.Reservations;
using StayFlow.Application.Reviews;
using StayFlow.Application.Rooms;

namespace StayFlow.Tests.Api;

public class ReviewTests : IClassFixture<StayFlowApiFactory>
{
    private readonly StayFlowApiFactory _factory;

    // Uygulama "bugun"u otelin saat dilimine gore hesapliyor (Europe/Istanbul)
    private static readonly DateOnly Today =
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul")).DateTime);

    public ReviewTests(StayFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Konaklama_bitince_bir_kez_yorum_yazilir_otel_yanitlar_ve_gizleyebilir()
    {
        var (owner, _) = await _factory.RegisterHotelAsync();
        var slug = (await owner.GetFromJsonAsync<Application.Hotels.HotelDto>("/api/hotel", TestJson.Options))!.Slug;
        var type = await (await owner.PostAsJsonAsync("/api/room-types", new { name = "Std", capacity = 2, basePrice = 1000 }))
            .Content.ReadFromJsonAsync<RoomTypeDto>(TestJson.Options);
        await owner.PostAsJsonAsync("/api/rooms", new { roomNumber = "101", floor = 1, roomTypeId = type!.Id });
        var guest = _factory.CreateBrowserLikeClient();

        // Bugun giris yapan misafir: rezervasyon + odeme
        var booking = await (await guest.PostAsJsonAsync($"/api/public/hotels/{slug}/reservations", new
        {
            roomTypeId = type.Id, checkIn = Today, checkOut = Today.AddDays(2), guestCount = 1,
            guest = new { fullName = "Ayşe Yılmaz", email = "ayse@test.com" }
        })).Content.ReadFromJsonAsync<GuestReservation>(TestJson.Options);
        var code = booking!.ConfirmationCode;
        await guest.PostAsJsonAsync($"/api/public/reservations/{code}/pay",
            new { email = "ayse@test.com", cardHolder = "A Y", cardNumber = "4242424242424242", expiry = "12/30", cvc = "123" });

        var review = new { email = "ayse@test.com", rating = 4, comment = "Temiz oda, güler yüzlü personel." };

        // Konaklama bitmeden yorum yok
        Assert.Equal(HttpStatusCode.Conflict, (await guest.PostAsJsonAsync($"/api/public/reservations/{code}/review", review)).StatusCode);

        // Resepsiyon: check-in ve (erken) check-out
        var reservationId = (await owner.GetFromJsonAsync<List<ReservationDto>>("/api/reservations", TestJson.Options))!.Single().Id;
        (await owner.PostAsync($"/api/reservations/{reservationId}/check-in", null)).EnsureSuccessStatusCode();
        (await owner.PostAsync($"/api/reservations/{reservationId}/check-out", null)).EnsureSuccessStatusCode();

        // Yorum yazilir, ikincisi reddedilir
        var afterReview = await (await guest.PostAsJsonAsync($"/api/public/reservations/{code}/review", review))
            .Content.ReadFromJsonAsync<GuestReservation>(TestJson.Options);
        Assert.Equal(4, afterReview!.MyRating);
        Assert.False(afterReview.CanReview);
        Assert.Equal(HttpStatusCode.Conflict, (await guest.PostAsJsonAsync($"/api/public/reservations/{code}/review", review)).StatusCode);

        // Herkese acik listede ad kisaltilmis
        var publicReviews = await guest.GetFromJsonAsync<List<PublicReview>>($"/api/public/hotels/{slug}/reviews", TestJson.Options);
        Assert.Equal("Ayşe Y.", Assert.Single(publicReviews!).GuestName);

        // Otel yanitlar
        var id = (await owner.GetFromJsonAsync<List<ReviewDto>>("/api/reviews", TestJson.Options))!.Single().Id;
        await owner.PutAsJsonAsync($"/api/reviews/{id}/reply", new { reply = "Teşekkür ederiz, tekrar bekleriz!" });
        publicReviews = await guest.GetFromJsonAsync<List<PublicReview>>($"/api/public/hotels/{slug}/reviews", TestJson.Options);
        Assert.Equal("Teşekkür ederiz, tekrar bekleriz!", publicReviews!.Single().OwnerReply);

        // Otel gizler: herkese acik listeden ve ortalamadan cikar
        await owner.PutAsJsonAsync($"/api/reviews/{id}/visibility", new { isPublished = false });
        Assert.Empty((await guest.GetFromJsonAsync<List<PublicReview>>($"/api/public/hotels/{slug}/reviews", TestJson.Options))!);
        var summary = await owner.GetFromJsonAsync<ReviewSummary>("/api/reviews/summary", TestJson.Options);
        Assert.Equal(0, summary!.Count);
        Assert.Null(summary.AverageRating);
    }

    [Theory]
    [InlineData("Ayşe Yılmaz", "Ayşe Y.")]
    [InlineData("Hans Peter Müller", "Hans M.")]
    [InlineData("Cher", "Cher")]
    [InlineData("ali ışık", "ali I.")]
    public void Misafir_adi_kisaltilir(string fullName, string expected) =>
        Assert.Equal(expected, GuestNames.Shorten(fullName));
}
