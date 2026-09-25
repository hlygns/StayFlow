using Microsoft.Extensions.DependencyInjection;
using StayFlow.Application.Auth;
using StayFlow.Application.Hotels;
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

        return services;
    }
}
