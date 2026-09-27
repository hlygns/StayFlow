using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Context;
using Serilog.Formatting.Compact;
using StayFlow.Infrastructure.Persistence;

namespace StayFlow.API.Infrastructure;

/// <summary>
/// Loglama ve izlenebilirlik: Serilog, istek basina correlation id, saglik kontrolleri.
/// Amac: canlida bir kullanici "hata aldim, kodu su" dediginde o istegin tum loglarini tek aramayla bulmak.
/// </summary>
public static class Observability
{
    public const string CorrelationHeader = "X-Correlation-Id";

    public static WebApplicationBuilder AddStayFlowLogging(this WebApplicationBuilder builder)
    {
        builder.Services.AddSerilog((services, config) =>
        {
            config
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "StayFlow.API");

            // Gelistirmede okunabilir metin; container'da tek satir JSON (log toplama araclari kolayca ayristirir)
            if (builder.Environment.IsDevelopment())
            {
                config.WriteTo.Console(outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
            }
            else
            {
                config.WriteTo.Console(new RenderedCompactJsonFormatter());
            }
        });

        // Tum ProblemDetails cevaplarina correlation id eklenir: kullanici hata ekraninda gordugu kodu iletebilir
        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier);

        // /health/live: surec ayakta mi (DB'ye bakmaz - DB gecici koptu diye container yeniden baslatilmasin)
        // /health/ready ve /health: veritabanina da erisilebiliyor mu (trafik alinabilir mi)
        builder.Services.AddHealthChecks().AddDbContextCheck<StayFlowDbContext>(tags: ["ready"]);

        return builder;
    }

    public static WebApplication UseStayFlowRequestLogging(this WebApplication app)
    {
        // 1) Correlation id: istemci gonderdiyse onu kullan, yoksa uret; cevaba ve tum loglara ekle
        app.Use(async (context, next) =>
        {
            var incoming = context.Request.Headers[CorrelationHeader].ToString();
            var correlationId = IsValid(incoming) ? incoming : Guid.NewGuid().ToString("N")[..16];
            context.TraceIdentifier = correlationId;
            // OnStarting: basligi cevap gonderilmeden hemen once ekle. Dogrudan eklenirse, bir hata
            // olustugunda exception handler cevabi sifirlarken basligi da siler - oysa correlation id'ye
            // en cok hata cevaplarinda ihtiyac var.
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[CorrelationHeader] = correlationId;
                return Task.CompletedTask;
            });

            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await next();
            }
        });

        // 2) Istek basina TEK ozet log satiri (yol, durum kodu, sure) + kim yapti
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} -> {StatusCode} ({Elapsed:0} ms)";
            options.EnrichDiagnosticContext = (diagnostics, context) =>
            {
                diagnostics.Set("HotelId", context.User.FindFirst("hotelId")?.Value);
                diagnostics.Set("UserId", context.User.FindFirst("sub")?.Value);
                diagnostics.Set("ClientIp", context.Connection.RemoteIpAddress?.ToString());
            };
            // Saglik kontrolleri her 10 sn'de bir gelir: loglari doldurmasin
            options.GetLevel = (context, _, exception) =>
                exception is not null || context.Response.StatusCode >= 500 ? Serilog.Events.LogEventLevel.Error
                : context.Request.Path.StartsWithSegments("/health") ? Serilog.Events.LogEventLevel.Verbose
                : Serilog.Events.LogEventLevel.Information;
        });

        return app;
    }

    public static WebApplication MapStayFlowHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
        app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
        return app;
    }

    // Log enjeksiyonunu onle: sadece kisa, harf/rakam/tire iceren degerleri kabul et
    private static bool IsValid(string value) =>
        value.Length is > 0 and <= 64 && value.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');
}
