using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StayFlow.Application.Common.Interfaces;
using StayFlow.Infrastructure.Email;
using StayFlow.Infrastructure.Persistence;
using StayFlow.Infrastructure.Security;

namespace StayFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, string connectionString, EmailSettings? emailSettings = null, string? redisConnectionString = null)
    {
        // EnableRetryOnFailure: SQL Server'a gecici baglanti hatalarinda (container yeni aciliyor,
        // ag kopuyor) istegi otomatik tekrar dener. Unique index ihlali gibi kalici hatalari TEKRARLAMAZ.
        services.AddDbContext<StayFlowDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(maxRetryCount: 5)));

        // Servisler IApplicationDbContext ister; ayni istek icindeki (scoped) StayFlowDbContext'i veriyoruz.
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<StayFlowDbContext>());
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<DemoDataSeeder>();

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
