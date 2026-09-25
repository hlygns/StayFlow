using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StayFlow.Application.Common.Interfaces;
using StayFlow.Infrastructure.Persistence;
using StayFlow.Infrastructure.Security;

namespace StayFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<StayFlowDbContext>(options => options.UseSqlServer(connectionString));

        // Servisler IApplicationDbContext ister; ayni istek icindeki (scoped) StayFlowDbContext'i veriyoruz.
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<StayFlowDbContext>());
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        return services;
    }
}
