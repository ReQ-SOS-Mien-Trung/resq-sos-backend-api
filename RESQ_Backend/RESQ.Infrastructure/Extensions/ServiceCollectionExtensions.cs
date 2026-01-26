using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Resources;
using RESQ.Infrastructure.Persistence.Base;
using RESQ.Infrastructure.Persistence.Context;
using RESQ.Infrastructure.Persistence.Resources;

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
            )
        );

        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Generic Repository   
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

        // Resources Repositories
        services.AddScoped<IDepotRepository, DepotRepository>();
        // Users Repositories
        return services;
    }
}
