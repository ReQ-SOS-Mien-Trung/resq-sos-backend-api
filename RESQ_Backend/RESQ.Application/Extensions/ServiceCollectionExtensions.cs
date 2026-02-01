using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using RESQ.Application.Behaviours;

namespace RESQ.Application.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(ServiceCollectionExtensions).Assembly);
        });

        // FluentValidation
        services.AddValidatorsFromAssembly(
            typeof(ServiceCollectionExtensions).Assembly
        );

        services.AddTransient(
            typeof(IPipelineBehavior<,>),
            typeof(ValidationBehaviour<,>)
        );

        return services;
    }
}
