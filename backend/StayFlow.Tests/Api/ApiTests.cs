using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Application.Auth;
using StayFlow.Application.Rooms;

namespace StayFlow.Tests.Api;

/// <summary>
/// HTTP katmaninin kurallari: kimlik dogrulama, roller, cookie, tenant izolasyonu, hata formati.
/// Servis testleri bunlari goremez - [Authorize] ve middleware'ler ancak gercek pipeline'da calisir.
/// </summary>
public class ApiTests : IClassFixture<StayFlowApiFactory>
{
    private readonly StayFlowApiFactory _factory;

    public ApiTests(StayFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Kayit_refresh_tokeni_govdede_degil_httpOnly_cookie_olarak_verir()
    {
        var client = _factory.CreateBrowserLikeClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            hotelName = "Cookie Otel", city = "Antalya", ownerFullName = "A B", ownerEmail = $"c-{Guid.NewGuid():N}@test.com", password = "Parola123!"
        });

        response.EnsureSuccessStatusCode();
        Assert.DoesNotContain("refreshToken", await response.Content.ReadAsStringAsync());
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("stayflow_refresh="));
        Assert.Contains("httponly", cookie);
        Assert.Contains("secure", cookie);
        Assert.Contains("samesite=strict", cookie);
        Assert.Contains("path=/api/auth", cookie);
    }

    [Fact]
    public async Task Cookie_ile_refresh_calisir_logouttan_sonra_calismaz()
    {
        var (client, _) = await _factory.RegisterHotelAsync();

        var refreshed = await client.PostAsync("/api/auth/refresh", null);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/auth/refresh", null)).StatusCode);
    }

    [Fact]
    public async Task Token_olmadan_korumali_uclar_401_doner()
    {
        var client = _factory.CreateBrowserLikeClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/rooms")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/reservations")).StatusCode);
    }

    [Fact]
    public async Task Resepsiyon_sahip_islemlerini_yapamaz_ama_okuyabilir()
    {
        var (owner, _) = await _factory.RegisterHotelAsync();
        var receptionEmail = $"rec-{Guid.NewGuid():N}@test.com";
        (await owner.PostAsJsonAsync("/api/users", new { fullName = "Resepsiyon", email = receptionEmail, password = "Parola123!", role = "Reception" }))
            .EnsureSuccessStatusCode();

        var reception = _factory.CreateBrowserLikeClient();
        var login = await reception.PostAsJsonAsync("/api/auth/login", new { email = receptionEmail, password = "Parola123!" });
        var token = (await login.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options))!.AccessToken;
        reception.DefaultRequestHeaders.Authorization = new("Bearer", token);

        Assert.Equal(HttpStatusCode.OK, (await reception.GetAsync("/api/room-types")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.PostAsJsonAsync("/api/room-types", new { name = "X", capacity = 2, basePrice = 100 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.PutAsJsonAsync("/api/hotel", new { name = "X", city = "Y" })).StatusCode);
    }

    [Fact]
    public async Task Baska_otelin_kaydi_404_doner()
    {
        var (hotelA, _) = await _factory.RegisterHotelAsync();
        var (hotelB, _) = await _factory.RegisterHotelAsync();

        var created = await hotelA.PostAsJsonAsync("/api/room-types", new { name = "Suit", capacity = 2, basePrice = 1000 });
        var roomType = (await created.Content.ReadFromJsonAsync<RoomTypeDto>(TestJson.Options))!;

        Assert.Equal(HttpStatusCode.OK, (await hotelA.GetAsync($"/api/room-types/{roomType.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await hotelB.GetAsync($"/api/room-types/{roomType.Id}")).StatusCode);
        Assert.Empty((await hotelB.GetFromJsonAsync<List<RoomTypeDto>>("/api/room-types", TestJson.Options))!);
    }

    [Fact]
    public async Task Cakisma_hatasi_409_ve_ProblemDetails_olarak_doner()
    {
        var (client, _) = await _factory.RegisterHotelAsync();
        var roomType = await (await client.PostAsJsonAsync("/api/room-types", new { name = "Std", capacity = 2, basePrice = 1000 }))
            .Content.ReadFromJsonAsync<RoomTypeDto>(TestJson.Options);
        var room = await (await client.PostAsJsonAsync("/api/rooms", new { roomNumber = "101", floor = 1, roomTypeId = roomType!.Id }))
            .Content.ReadFromJsonAsync<RoomDto>(TestJson.Options);

        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);
        var booking = new { roomId = room!.Id, checkIn, checkOut = checkIn.AddDays(2), guest = new { fullName = "Misafir" } };
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/reservations", booking)).StatusCode);

        var conflict = await client.PostAsJsonAsync("/api/reservations", booking);

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("application/problem+json", conflict.Content.Headers.ContentType?.MediaType);
        var problem = await conflict.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Secilen oda bu tarihlerde musait degil.", problem!.Detail);
    }

    [Fact]
    public async Task Gecersiz_istek_alan_bazinda_400_doner()
    {
        var (client, _) = await _factory.RegisterHotelAsync();

        var response = await client.PostAsJsonAsync("/api/room-types", new { name = "", capacity = 0, basePrice = -5 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("Name", problem!.Errors.Keys);
        Assert.Contains("Capacity", problem.Errors.Keys);
        Assert.Contains("BasePrice", problem.Errors.Keys);
    }

    [Fact]
    public async Task Her_cevapta_correlation_id_var_ve_hata_govdesinde_de_doner()
    {
        var client = _factory.CreateBrowserLikeClient();
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "destek-talebi-42");

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "yok@test.com", password = "yanlis" });

        Assert.Equal("destek-talebi-42", response.Headers.GetValues("X-Correlation-Id").Single());
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("destek-talebi-42", problem!.Extensions["correlationId"]?.ToString());
    }

    [Fact]
    public async Task Gecersiz_correlation_id_yok_sayilir_yenisi_uretilir()
    {
        var client = _factory.CreateBrowserLikeClient();
        // Bosluk ve ozel karakter iceren deger loglara yazilmamali (log enjeksiyonu)
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Correlation-Id", "kotu deger; sahte=log <script>");

        var response = await client.GetAsync("/health/live");

        var id = response.Headers.GetValues("X-Correlation-Id").Single();
        Assert.DoesNotContain("sahte", id);
        Assert.Equal(16, id.Length);
    }

    [Fact]
    public async Task Health_endpointi_veritabani_ile_birlikte_saglikli()
    {
        var response = await _factory.CreateBrowserLikeClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}

/// <summary>Deneme sinirini ayri bir uygulama ornegiyle (dakikada 3) test ediyoruz.</summary>
public class RateLimitTests : IClassFixture<RateLimitTests.StrictFactory>
{
    public class StrictFactory : StayFlowApiFactory
    {
        protected override int AuthPermitPerMinute => 3;
    }

    private readonly StrictFactory _factory;

    public RateLimitTests(StrictFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Sinir_asilinca_429_ve_RetryAfter_doner()
    {
        var client = _factory.CreateBrowserLikeClient();
        var attempt = new { email = "yok@test.com", password = "yanlis" };

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", attempt)).StatusCode);
        }

        var blocked = await client.PostAsJsonAsync("/api/auth/login", attempt);

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.True(blocked.Headers.Contains("Retry-After"));
    }
}
