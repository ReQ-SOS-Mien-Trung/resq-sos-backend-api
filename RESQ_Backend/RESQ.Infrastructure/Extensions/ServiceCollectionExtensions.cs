using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Repositories.System;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Services;
using RESQ.Application.Repositories.Emergency;
using RESQ.Infrastructure.Persistence.Base;
using RESQ.Infrastructure.Persistence.Context;
using RESQ.Infrastructure.Persistence.Logistics;
using RESQ.Infrastructure.Persistence.Identity;
using RESQ.Infrastructure.Persistence.Emergency;
using RESQ.Infrastructure.Persistence.Personnel;
using RESQ.Infrastructure.Persistence.System;
using RESQ.Infrastructure.Persistence.Finance;
using RESQ.Infrastructure.Services;
using RESQ.Infrastructure.Services.Payments;

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
        services.AddScoped<IItemCategoryRepository, ItemCategoryRepository>();

        // Personnel Repositories
        services.AddScoped<IAssemblyPointRepository, AssemblyPointRepository>(); 

        // Users Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRescuerApplicationRepository, RescuerApplicationRepository>();
        services.AddScoped<IAbilityRepository, AbilityRepository>();
        services.AddScoped<IDocumentFileTypeRepository, DocumentFileTypeRepository>();

        // Emergency Repositories
        services.AddScoped<ISosRequestRepository, SosRequestRepository>();
        services.AddScoped<ISosRuleEvaluationRepository, SosRuleEvaluationRepository>();
        services.AddScoped<ISosAiAnalysisRepository, SosAiAnalysisRepository>();

        // Finance Repositories
        services.AddScoped<IFundCampaignRepository, FundCampaignRepository>();
        services.AddScoped<IDonationRepository, DonationRepository>();
        services.AddScoped<IFundTransactionRepository, FundTransactionRepository>();
        services.AddScoped<IDepotFundAllocationRepository, DepotFundAllocationRepository>();

        // System Repositories
        services.AddScoped<IPromptRepository, PromptRepository>();

        // Services
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<ISosPriorityEvaluationService, SosPriorityEvaluationService>();
        services.AddScoped<ISosAiAnalysisService, SosAiAnalysisService>();
        services.AddScoped<IAiModelTestService, AiModelTestService>();
        services.AddScoped<IRescueMissionSuggestionService, RescueMissionSuggestionService>();
        
        // Payment Service
        services.AddScoped<IPaymentGatewayService, PayOSService>();

        // Background Services
        services.AddSingleton<SosAiAnalysisQueue>();
        services.AddSingleton<ISosAiAnalysisQueue>(sp => sp.GetRequiredService<SosAiAnalysisQueue>());
        services.AddSingleton<ISosAiAnalysisQueueInternal>(sp => sp.GetRequiredService<SosAiAnalysisQueue>());
        services.AddHostedService<SosAiAnalysisBackgroundService>();

        return services;
    }
}
