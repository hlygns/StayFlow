using Microsoft.Extensions.DependencyInjection;
using StayFlow.Application.Auth;
using StayFlow.Application.Hotels;
using StayFlow.Application.Reservations;
using StayFlow.Application.Rooms;
using StayFlow.Application.Users;

namespace StayFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<HotelService>();
        services.AddScoped<RoomTypeService>();
        services.AddScoped<RoomService>();
        services.AddScoped<SeasonalPriceService>();
        services.AddScoped<AvailabilityService>();
        services.AddScoped<ReservationService>();

        // "Simdi" bilgisini DateTime.UtcNow yerine TimeProvider'dan aliyoruz: testlerde
        // (ve Faz 2'de Hangfire islerinde) zamani kontrol edebilmek icin.
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
