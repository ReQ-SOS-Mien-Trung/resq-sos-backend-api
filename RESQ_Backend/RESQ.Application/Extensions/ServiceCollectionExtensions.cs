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
            
            // Register Behaviours in the pipeline order:
            // 1. UnhandledExceptionBehaviour (Logs critical errors)
            // 2. LoggingBehaviour (Logs request entry/exit)
            // 3. ValidationBehaviour (Validates Input DTOs)
            // 4. DomainExceptionBehaviour (Maps Business Rules to BadRequest)
            
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(DomainExceptionBehaviour<,>));
        });

        // FluentValidation
        services.AddValidatorsFromAssembly(
            typeof(ServiceCollectionExtensions).Assembly
        );

        // Note: The AddTransient calls below are legacy syntax if using cfg.AddBehavior above, 
        // but kept if you are not using MediatR 12+ built-in registration fully or supporting older patterns.
        // If you are using MediatR 12.0+, the cfg.AddBehavior lines inside AddMediatR are preferred.
        // Assuming we keep the existing style but ensure DomainExceptionBehaviour is added:
        
        // Ensure DomainExceptionBehaviour is registered
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(DomainExceptionBehaviour<,>));

        return services;
    }
}
