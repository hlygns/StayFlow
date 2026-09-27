using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StayFlow.Application.BackgroundJobs;
using StayFlow.Application.Common.Interfaces;
using StayFlow.Application.Common;
using StayFlow.Application.Auth;
using StayFlow.Application.Hotels;
using StayFlow.Application.Reservations;
using StayFlow.Application.Rooms;
using StayFlow.Application.Users;

namespace StayFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, string hotelTimeZone = "Europe/Istanbul")
    {
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<HotelService>();
        services.AddScoped<RoomTypeService>();
        services.AddScoped<RoomService>();
        services.AddScoped<SeasonalPriceService>();
        services.AddScoped<AvailabilityService>();
        services.AddSingleton<AvailabilityCache>();
        services.AddSingleton<IAvailabilityCacheInvalidator>(sp => sp.GetRequiredService<AvailabilityCache>());
        services.AddScoped<ReservationService>();
        services.AddScoped<StayFlow.Application.Public.PublicBookingService>();
        services.AddScoped<StayFlow.Application.Reviews.ReviewService>();

        // Arka plan isleri (Hangfire bunlari DI'dan alip calistirir)
        services.AddScoped<ExpiredReservationCanceller>();
        services.AddScoped<ArrivalReminderJob>();
        services.AddScoped<DailyReportJob>();

        // Varsayilan: bildirim yok. API katmani SignalR uygulamasini kaydederek bunu degistirir.
        services.TryAddSingleton<IReservationNotifier>(NullReservationNotifier.Instance);

        // "Simdi" bilgisini DateTime.UtcNow yerine TimeProvider'dan aliyoruz: testlerde
        // (ve Faz 2'de Hangfire islerinde) zamani kontrol edebilmek icin. "Bugun" otelin saat dilimine gore.
        services.AddSingleton<TimeProvider>(new HotelTimeProvider(TimeZoneInfo.FindSystemTimeZoneById(hotelTimeZone)));

        return services;
    }
}
