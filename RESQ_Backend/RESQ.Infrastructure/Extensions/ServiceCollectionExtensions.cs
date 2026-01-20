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
        return services;
    }
}
