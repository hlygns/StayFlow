using Microsoft.Extensions.DependencyInjection;
using StayFlow.Application.Auth;
using StayFlow.Application.Hotels;
using StayFlow.Application.Users;

namespace StayFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<HotelService>();

        return services;
    }
}
