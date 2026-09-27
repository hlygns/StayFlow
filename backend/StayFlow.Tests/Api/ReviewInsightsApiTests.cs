using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StayFlow.Application.Auth;
using StayFlow.Application.Reservations;
using StayFlow.Application.Reviews;
using StayFlow.Application.Rooms;
using StayFlow.Domain.Entities;
using StayFlow.Infrastructure.Persistence;

namespace StayFlow.Tests.Api;

/// <summary>Anahtar olmadan: ozellik kapali (503). Ayri bir uygulama ornegi, cunku asistan DI'da tek.</summary>
public class ReviewInsightsWithoutKeyTests : IClassFixture<StayFlowApiFactory>
{
    private readonly StayFlowApiFactory _factory;

    public ReviewInsightsWithoutKeyTests(StayFlowApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Anahtar_yoksa_ozellik_kapali_ve_503_doner()
    {
        var (owner, _) = await _factory.RegisterHotelAsync();

        var status = await owner.GetFromJsonAsync<Dictionary<string, bool>>("/api/reviews/ai-status");
        var insights = await owner.GetAsync("/api/reviews/insights");

        Assert.False(status!["available"]);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, insights.StatusCode);
    }
}

public class ReviewInsightsApiTests : IClassFixture<ReviewInsightsApiTests.FactoryWithFakeAi>
{
    public class FakeAssistant : IReviewAssistant
    {
        public int SummaryCalls;
        public bool IsConfigured => true;

        public Task<ReviewInsights> SummarizeAsync(string hotelName, IReadOnlyList<ReviewForAi> reviews, CancellationToken ct = default)
        {
            Interlocked.Increment(ref SummaryCalls);
            return Task.FromResult(new ReviewInsights($"{reviews.Count} yorum özeti", ["Kahvaltı"], ["Wi-Fi"], ["Wi-Fi yenilenmeli"], reviews.Count, DateTime.UtcNow));
        }

        public Task<string> SuggestReplyAsync(string hotelName, ReviewForAi review, CancellationToken ct = default) =>
            Task.FromResult($"Teşekkürler ({review.Rating} yıldız) — {hotelName}");
    }

    public class FactoryWithFakeAi : StayFlowApiFactory
    {
        public FakeAssistant Assistant { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IReviewAssistant>();
                services.AddSingleton<IReviewAssistant>(Assistant);
            });
        }
    }

    private readonly FactoryWithFakeAi _factory;

    public ReviewInsightsApiTests(FactoryWithFakeAi factory) => _factory = factory;

    /// <summary>Yorumlari dogrudan veritabanina ekler (check-out akisini burada tekrar test etmeye gerek yok).</summary>
    private async Task AddReviewsAsync(HttpClient owner, AuthResponse auth, int count, int startDay)
    {
        var type = await (await owner.PostAsJsonAsync("/api/room-types", new { name = $"T{startDay}", capacity = 2, basePrice = 1000 }))
            .Content.ReadFromJsonAsync<RoomTypeDto>(TestJson.Options);
        var room = await (await owner.PostAsJsonAsync("/api/rooms", new { roomNumber = $"R{startDay}", floor = 1, roomTypeId = type!.Id }))
            .Content.ReadFromJsonAsync<RoomDto>(TestJson.Options);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StayFlowDbContext>();
        for (var i = 0; i < count; i++)
        {
            var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(startDay + i * 2);
            var reservation = new Reservation
            {
                HotelId = auth.User.HotelId, RoomId = room!.Id, CheckIn = checkIn, CheckOut = checkIn.AddDays(1),
                Guest = new Guest { HotelId = auth.User.HotelId, FullName = "Misafir" }
            };
            db.Reservations.Add(reservation);
            db.Reviews.Add(new Review { HotelId = auth.User.HotelId, Reservation = reservation, Rating = 4, Comment = $"Yorum {startDay}-{i}", GuestName = "Misafir" });
        }
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Ozet_onbellekten_gelir_yeni_yorum_gelince_yenilenir()
    {
        var (owner, auth) = await _factory.RegisterHotelAsync();

        // 3'ten az yorumla ozet yok
        await AddReviewsAsync(owner, auth, 2, 10);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync("/api/reviews/insights")).StatusCode);

        await AddReviewsAsync(owner, auth, 2, 30);
        var before = _factory.Assistant.SummaryCalls;
        var first = await owner.GetFromJsonAsync<ReviewInsights>("/api/reviews/insights");
        var second = await owner.GetFromJsonAsync<ReviewInsights>("/api/reviews/insights");

        Assert.Equal("4 yorum özeti", first!.Summary);
        Assert.Equal(first.GeneratedAt, second!.GeneratedAt);
        Assert.Equal(before + 1, _factory.Assistant.SummaryCalls); // ikinci istek ucretli cagri yapmadi

        await AddReviewsAsync(owner, auth, 1, 50);
        var third = await owner.GetFromJsonAsync<ReviewInsights>("/api/reviews/insights");
        Assert.Equal("5 yorum özeti", third!.Summary);
        Assert.Equal(before + 2, _factory.Assistant.SummaryCalls);
    }

    [Fact]
    public async Task Yanit_onerisi_doner_baska_otelin_yorumu_404()
    {
        var (owner, auth) = await _factory.RegisterHotelAsync();
        var (other, _) = await _factory.RegisterHotelAsync();
        await AddReviewsAsync(owner, auth, 1, 70);
        var reviewId = (await owner.GetFromJsonAsync<List<ReviewDto>>("/api/reviews", TestJson.Options))!.Single().Id;

        var suggestion = await (await owner.PostAsync($"/api/reviews/{reviewId}/suggest-reply", null)).Content.ReadFromJsonAsync<Dictionary<string, string>>();

        Assert.Equal("Teşekkürler (4 yıldız) — Test Otel", suggestion!["reply"]);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/reviews/{reviewId}/suggest-reply", null)).StatusCode);
    }
}
