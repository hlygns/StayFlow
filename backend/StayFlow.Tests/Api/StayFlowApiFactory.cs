using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StayFlow.Application.Auth;
using StayFlow.Infrastructure.Persistence;

namespace StayFlow.Tests.Api;

/// <summary>
/// Gercek uygulamayi (Program.cs: DI, middleware, JWT, rate limit, hata yonetimi...) bellekte calistirir.
/// Tek fark: SQL Server yerine bellekte SQLite. Istekler gercek HTTP pipeline'indan gecer.
/// </summary>
public class StayFlowApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    /// <summary>Testlerin cogu ayni "IP"den cok giris yapar; varsayilan sinira takilmasin.</summary>
    protected virtual int AuthPermitPerMinute => 1000;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Issuer", "StayFlow");
        builder.UseSetting("Jwt:Audience", "StayFlow.Clients");
        builder.UseSetting("Jwt:SecretKey", "integration-tests-only-secret-key-0123456789");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=unused");
        builder.UseSetting("RateLimiting:AuthPermitPerMinute", AuthPermitPerMinute.ToString());
        builder.UseSetting("BackgroundJobs:Enabled", "false"); // Hangfire SQL Server ister; is mantigi ayri test ediliyor

        builder.ConfigureServices(services =>
        {
            // AddInfrastructure'in SQL Server kaydini tamamen kaldirip SQLite ile degistir
            services.RemoveAll<DbContextOptions<StayFlowDbContext>>();
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<StayFlowDbContext>));
            services.AddDbContext<StayFlowDbContext>(options => options.UseSqlite(_connection));
        });
    }

    /// <summary>
    /// Refresh cookie "Secure" oldugu icin istemci https adresiyle konusmali (TestServer icin sadece bir etiket).
    /// Cookie'ler bir tarayicidaki gibi istemci tarafinda saklanip otomatik gonderilir.
    /// </summary>
    public HttpClient CreateBrowserLikeClient()
    {
        EnsureDatabase();
        return CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
    }

    private bool _created;

    private void EnsureDatabase()
    {
        if (_created) return;
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<StayFlowDbContext>().Database.EnsureCreated();
        _created = true;
    }

    /// <summary>Yeni bir otel kaydeder, access token'i istemcinin Authorization basligina koyar.</summary>
    public async Task<(HttpClient Client, AuthResponse Auth)> RegisterHotelAsync(string? email = null)
    {
        var client = CreateBrowserLikeClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            hotelName = "Test Otel",
            city = "Antalya",
            ownerFullName = "Test Sahibi",
            ownerEmail = email ?? $"owner-{Guid.NewGuid():N}@test.com",
            password = "Parola123!"
        });
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options))!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        return (client, auth);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}

/// <summary>API enum'lari isim olarak dondurur ("Owner"); testte de ayni ayarla okuyoruz.</summary>
public static class TestJson
{
    public static readonly System.Text.Json.JsonSerializerOptions Options = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };
}
