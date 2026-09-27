using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using StayFlow.API.Infrastructure;
using StayFlow.API.Realtime;
using StayFlow.API.Services;
using StayFlow.Application;
using StayFlow.Application.Common.Interfaces;
using StayFlow.Application.Common.Models;
using StayFlow.Infrastructure;
using StayFlow.Infrastructure.Email;
using StayFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.AddStayFlowLogging();

// Enum'lar JSON'da sayi yerine isim olarak gorunsun: "role": "Owner" (0 degil).
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
// OpenAPI dokumani: /openapi/v1.json. Belgeye "Bearer" guvenlik semasi ekleniyor ki
// dokumantasyon sayfasindan JWT ile istek atilabilsin.
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "StayFlow API";
    document.Info.Description = "Butik oteller için çok kiracılı rezervasyon API'si. Önce /api/auth/login ile token alın.";
    document.Components ??= new();
    document.Components.SecuritySchemes ??= new Dictionary<string, Microsoft.OpenApi.IOpenApiSecurityScheme>();
    document.Components.SecuritySchemes["Bearer"] = new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    };
    return Task.CompletedTask;
}));

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentTenantService, CurrentTenantService>();

// Otellerin saat dilimi (IANA adi). Simdilik tum oteller Turkiye'de; ileride otel bazina tasinabilir.
var hotelTimeZone = TimeZoneInfo.FindSystemTimeZoneById(builder.Configuration["App:TimeZone"] ?? "Europe/Istanbul");
builder.Services.AddApplication(hotelTimeZone.Id);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection tanimli degil.");
builder.Services.AddInfrastructure(
    connectionString,
    builder.Configuration.GetSection("Email").Get<EmailSettings>(),
    builder.Configuration["Redis:ConnectionString"]);
builder.Services.AddStayFlowBackgroundJobs(builder.Configuration, connectionString);

// === JWT ===
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
// HS256 icin anahtar en az 256 bit (32 byte) olmali. Anahtar appsettings.json'da DEGIL;
// gelistirmede appsettings.Development.json / user-secrets, canlida ortam degiskeni (Jwt__SecretKey).
if (Encoding.UTF8.GetByteCount(jwtSettings.SecretKey) < 32)
{
    throw new InvalidOperationException("Jwt:SecretKey tanimli degil veya 32 byte'tan kisa.");
}
// .env.example'daki ornek deger 32 karakterden uzun: kopyalanip degistirilmeden canliya cikmasin.
if (jwtSettings.SecretKey.StartsWith("buraya-", StringComparison.Ordinal))
{
    throw new InvalidOperationException("Jwt:SecretKey hala .env.example'daki ornek deger. 'openssl rand -base64 48' ile yeni bir anahtar uretin.");
}

builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Claim adlarini token'daki gibi birak ("sub", "role", "hotelId") - bkz. JwtTokenService.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = "role"
        };

        // WebSocket baglantisinda tarayici Authorization basligi gonderemez; SignalR token'i
        // ?access_token=... ile iletir. Bunu SADECE hub adresinde kabul ediyoruz.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments(ReservationsHub.Path))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// Canli bildirimler (SignalR). Application'daki varsayilan "bildirim yok" kaydinin yerine gecer.
builder.Services.AddSignalR()
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddSingleton<IReservationNotifier, SignalRReservationNotifier>();
builder.Services.AddStayFlowRateLimiting(builder.Configuration);

// React yonetim paneli (2. hafta) farkli bir porttan istek atacak. Izinli adresler config'ten gelir.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

// Canlida API bir reverse proxy'nin (Nginx) arkasinda calisir: HTTPS orada biter, API'ye HTTP gelir.
// Proxy'nin ekledigi X-Forwarded-* basliklarindan istemcinin gercek IP'si ve semasi (https) okunur.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Proxy ayni Docker agindaki container; IP'si sabit olmadigi icin bilinen proxy listesi bos birakiliyor.
    // API portu disariya ACILMAMALI (bkz. docker-compose.prod.yml), yoksa bu basliklar taklit edilebilir.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

// Container ilk acildiginda veritabani semasini olustur/guncelle. Tek API instance'i oldugu surece
// guvenli; birden fazla instance'a gecilirse migration ayri bir adimda (migration bundle) calismali.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<StayFlowDbContext>().Database.MigrateAsync();
}

// Canli demo: giris ekranindaki "Demo hesabiyla dene" butonu bu oteli kullanir.
if (app.Configuration.GetValue<bool>("Database:SeedDemoData"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();
}

// Zamanlanmis isler veritabani hazir olduktan sonra kaydedilir (Hangfire tablolari ayni DB'de)
app.UseStayFlowBackgroundJobs(hotelTimeZone);

app.UseStayFlowRequestLogging();
app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // Etkilesimli API dokumantasyonu: http://localhost:5182/scalar
    app.MapScalarApiReference(options => options.WithTitle("StayFlow API").AddPreferredSecuritySchemes("Bearer"));
}

// HTTPS yonlendirmesi sadece gelistirmede: canlida HTTPS'i Nginx yonetiyor.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapStayFlowHealthChecks();
app.MapHub<ReservationsHub>(ReservationsHub.Path);

app.Run();

// Integration testlerde WebApplicationFactory<Program> ile erisebilmek icin.
public partial class Program { }
