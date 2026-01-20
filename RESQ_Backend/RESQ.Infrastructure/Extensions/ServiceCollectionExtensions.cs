using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    // Lớp này khai báo DI cho infrastructure layer
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient();

        // DbContext Configuration
        services.AddDbContext<ResQDbContext>(options =>
        options.UseNpgsql(
        configuration.GetConnectionString("ResQDb"),
        x => x.UseNetTopologySuite()
    ));
        // Generic Repositories

        // Users Repositories
        services.AddScoped<RESQ.Domain.Repositories.IUserRepository, RESQ.Infrastructure.Persistence.Users.UserRepository>();

        // Token service
        services.AddScoped<RESQ.Application.Services.ITokenService, RESQ.Infrastructure.Services.TokenService>();

        return services;
    }
}
