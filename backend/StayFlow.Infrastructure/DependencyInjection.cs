using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StayFlow.Application.Common.Interfaces;
using Anthropic;
using StayFlow.Application.Reviews;
using StayFlow.Infrastructure.Ai;
using StayFlow.Infrastructure.Email;
using StayFlow.Infrastructure.Persistence;
using StayFlow.Infrastructure.Security;

namespace StayFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, string connectionString, EmailSettings? emailSettings = null, string? redisConnectionString = null,
        string? anthropicApiKey = null, string? anthropicBaseUrl = null)
    {
        // EnableRetryOnFailure: SQL Server'a gecici baglanti hatalarinda (container yeni aciliyor,
        // ag kopuyor) istegi otomatik tekrar dener. Unique index ihlali gibi kalici hatalari TEKRARLAMAZ.
        services.AddDbContext<StayFlowDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(maxRetryCount: 5)));

        // Servisler IApplicationDbContext ister; ayni istek icindeki (scoped) StayFlowDbContext'i veriyoruz.
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<StayFlowDbContext>());
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<DemoDataSeeder>();

        // Yapay zeka (yorum ozeti / yanit onerisi): anahtar yoksa ozellik kapali, sistemin geri kalani etkilenmez
        if (string.IsNullOrWhiteSpace(anthropicApiKey))
        {
            services.AddSingleton<IReviewAssistant, UnconfiguredReviewAssistant>();
        }
        else
        {
            var client = string.IsNullOrWhiteSpace(anthropicBaseUrl)
                ? new AnthropicClient { ApiKey = anthropicApiKey, Timeout = TimeSpan.FromSeconds(90) }
                // Kurumsal proxy veya yerel test sunucusu
                : new AnthropicClient { ApiKey = anthropicApiKey, Timeout = TimeSpan.FromSeconds(90), BaseUrl = anthropicBaseUrl };
            services.AddSingleton(client);
            services.AddSingleton<IReviewAssistant, ClaudeReviewAssistant>();
        }

        // Dagitik onbellek: Redis ayarliysa Redis (birden fazla API instance'i ayni onbellegi paylasir),
        // degilse bellek ici (tek instance / gelistirme / test)
        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddDistributedMemoryCache();
        }
        else
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "stayflow:";
            });
        }

        // SMTP ayarliysa gercek gonderim, degilse sadece log (testler ve SMTP'siz kurulumlar icin)
        emailSettings ??= new EmailSettings();
        if (string.IsNullOrWhiteSpace(emailSettings.Host))
        {
            services.AddSingleton<IEmailSender, LoggingEmailSender>();
        }
        else
        {
            services.AddSingleton(emailSettings);
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        }

        return services;
    }
}
