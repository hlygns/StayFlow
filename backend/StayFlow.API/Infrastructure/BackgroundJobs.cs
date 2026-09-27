using Hangfire;
using Hangfire.SqlServer;
using StayFlow.Application.BackgroundJobs;
using StayFlow.Infrastructure.Persistence;

namespace StayFlow.API.Infrastructure;

/// <summary>
/// Hangfire: zamanlanmis isler. Is durumlari SQL Server'da (ayni veritabani, "HangFire" semasi) tutulur;
/// uygulama yeniden baslasa da is gecmisi ve zamanlamalar kaybolmaz.
/// Is mantigi Application katmanindaki siniflarda - Hangfire sadece "ne zaman calissin" sorusunu cevaplar.
/// </summary>
public static class BackgroundJobs
{
    public static IServiceCollection AddStayFlowBackgroundJobs(this IServiceCollection services, IConfiguration configuration, string connectionString)
    {
        if (!IsEnabled(configuration))
        {
            return services;
        }

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
            {
                PrepareSchemaIfNecessary = true,
                QueuePollInterval = TimeSpan.FromSeconds(15)
            }));

        services.AddHangfireServer(options => options.WorkerCount = 2);
        return services;
    }

    public static WebApplication UseStayFlowBackgroundJobs(this WebApplication app, TimeZoneInfo hotelTimeZone)
    {
        if (!IsEnabled(app.Configuration))
        {
            return app;
        }

        var jobs = app.Services.GetRequiredService<IRecurringJobManager>();
        var hotelTime = new RecurringJobOptions { TimeZone = hotelTimeZone };

        // Her dakika: odeme suresi dolan rezervasyonlari iptal et
        jobs.AddOrUpdate<ExpiredReservationCanceller>("odeme-suresi-dolan-rezervasyonlar",
            job => job.RunAsync(CancellationToken.None), Cron.Minutely());

        // Her sabah 10:00 (otel saati): yarin gelecek misafirlere hatirlatma
        jobs.AddOrUpdate<ArrivalReminderJob>("giris-hatirlatmasi",
            job => job.RunAsync(CancellationToken.None), Cron.Daily(10), hotelTime);

        // Her aksam 22:00 (otel saati): otel sahiplerine gun sonu raporu
        jobs.AddOrUpdate<DailyReportJob>("gun-sonu-raporu",
            job => job.RunAsync(CancellationToken.None), Cron.Daily(22), hotelTime);

        // Canli demo: her gece 04:00'te demo otel bugune gore yeniden olusturulur
        if (app.Configuration.GetValue<bool>("Database:SeedDemoData"))
        {
            jobs.AddOrUpdate<DemoDataSeeder>("demo-verisini-yenile",
                job => job.ResetAsync(CancellationToken.None), Cron.Daily(4), hotelTime);
        }

        // Yonetim ekrani sadece gelistirmede (varsayilan yetki: sadece localhost'tan erisim)
        if (app.Environment.IsDevelopment())
        {
            app.MapHangfireDashboard("/hangfire");
        }

        return app;
    }

    private static bool IsEnabled(IConfiguration configuration) => configuration.GetValue("BackgroundJobs:Enabled", true);
}
