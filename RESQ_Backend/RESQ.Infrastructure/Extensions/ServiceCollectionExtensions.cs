using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Personnel; // Added
using RESQ.Application.Services;
using RESQ.Application.Repositories.SosRequests;
using RESQ.Infrastructure.Persistence.Base;
using RESQ.Infrastructure.Persistence.Context;
using RESQ.Infrastructure.Persistence.Logistics;
using RESQ.Infrastructure.Persistence.Identity;
using RESQ.Infrastructure.Persistence.Personnel; // Added
using RESQ.Infrastructure.Persistence.SosRequests;
using RESQ.Infrastructure.Services;

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

        // Personnel Repositories
        services.AddScoped<IAssemblyPointRepository, AssemblyPointRepository>(); // Registered

        // Users Repositories
        services.AddScoped<IUserRepository, UserRepository>();

        // SosRequests Repositories
        services.AddScoped<ISosRequestRepository, SosRequestRepository>();

        // Services
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IEmailService, EmailService>();

        return services;
    }
}
