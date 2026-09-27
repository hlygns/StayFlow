using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace StayFlow.API.Infrastructure;

public static class RateLimitPolicies
{
    public const string Auth = "auth";
    public const string Public = "public";
    public const string Ai = "ai";

    /// <summary>
    /// Giris ve kayit uclari icin IP basina dakikada N istek (varsayilan 20). Sifre deneme-yanilma
    /// (brute force) ve toplu sahte hesap acmayi yavaslatir. Asilinca 429 + ProblemDetails doner.
    /// </summary>
    public static IServiceCollection AddStayFlowRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permitPerMinute = configuration.GetValue("RateLimiting:AuthPermitPerMinute", 20);

        return services.AddRateLimiter(options =>
        {
            options.AddPolicy(Auth, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                // UseForwardedHeaders sayesinde bu, Nginx arkasinda da istemcinin gercek IP'si
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

            // Misafir uygulamasi (oturumsuz): IP basina dakikada 120 istek. Rezervasyon kodu tahmin etmeye
            // (kod + e-posta birlikte gerekse de) ve musaitlik sorgusuyla sunucuyu yormaya karsi.
            var publicPermit = configuration.GetValue("RateLimiting:PublicPermitPerMinute", 120);
            options.AddPolicy(Public, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions { PermitLimit = publicPermit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

            // Yapay zeka cagrilari ucretli: otel basina saatte 30 istek (ozet zaten onbellekte)
            var aiPermit = configuration.GetValue("RateLimiting:AiPermitPerHour", 30);
            options.AddPolicy(Ai, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.User.FindFirst("hotelId")?.Value ?? "anonymous",
                factory: _ => new FixedWindowRateLimiterOptions { PermitLimit = aiPermit, Window = TimeSpan.FromHours(1), QueueLimit = 0 }));

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                }

                await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Cok fazla deneme",
                    Detail = "Çok fazla deneme yapıldı. Lütfen bir dakika sonra tekrar deneyin."
                }, ct);
            };
        });
    }
}
