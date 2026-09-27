using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using StayFlow.Application.Auth;
using StayFlow.Tests.Api;
using Testcontainers.MsSql;
using Testcontainers.Redis;

namespace StayFlow.Tests.Containers;

/// <summary>
/// Gercek altyapi: Docker'da SQL Server 2022 ve Redis 7 container'lari (Testcontainers). Uygulama,
/// canlidakiyle AYNI veritabani motoru, migration'lar, retry stratejisi ve Redis onbellegiyle calisir.
/// SQLite testlerinin kanitlayamayacagi seyleri kanitlar: SQL Server'in kilitlenme davranisi,
/// gercek es zamanli transaction'lar, migration'larin temiz uygulanmasi.
///
/// Docker gerektirir; container'lar test sinifi basina bir kez baslatilir ve sonunda silinir.
/// </summary>
public sealed class RealInfrastructureFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlServer = new MsSqlBuilder().WithImage("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private readonly RedisContainer _redis = new RedisBuilder().WithImage("redis:7.4-alpine").Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_sqlServer.StartAsync(), _redis.StartAsync());

        var connectionString = _sqlServer.GetConnectionString().Replace("Database=master", "Database=StayFlowTests");
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
            builder.UseSetting("Redis:ConnectionString", _redis.GetConnectionString());
            builder.UseSetting("Database:MigrateOnStartup", "true"); // gercek migration'lar uygulanir
            builder.UseSetting("Jwt:Issuer", "StayFlow");
            builder.UseSetting("Jwt:Audience", "StayFlow.Clients");
            builder.UseSetting("Jwt:SecretKey", "container-tests-only-secret-key-0123456789");
            builder.UseSetting("RateLimiting:AuthPermitPerMinute", "1000");
            builder.UseSetting("BackgroundJobs:Enabled", "false");
        });
        _ = Factory.Server; // uygulamayi (ve migration'lari) simdi baslat
    }

    public HttpClient CreateClient() =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });

    public async Task<HttpClient> RegisterHotelAsync()
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            hotelName = "Container Otel", city = "Antalya", ownerFullName = "Test", ownerEmail = $"c-{Guid.NewGuid():N}@test.com", password = "Parola123!"
        });
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options);
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth!.AccessToken);
        return client;
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await Task.WhenAll(_sqlServer.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask());
    }
}
