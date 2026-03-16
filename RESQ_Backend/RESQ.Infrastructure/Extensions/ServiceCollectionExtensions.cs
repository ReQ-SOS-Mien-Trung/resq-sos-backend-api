using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Finance.Services;
using RESQ.Domain.Entities.Logistics.Services;
using RESQ.Infrastructure.Persistence.Base;
using RESQ.Infrastructure.Persistence.Context;
using RESQ.Infrastructure.Persistence.Emergency;
using RESQ.Infrastructure.Persistence.Finance;
using RESQ.Infrastructure.Persistence.Identity;
using RESQ.Infrastructure.Persistence.Logistics;
using RESQ.Infrastructure.Persistence.Operations;
using RESQ.Infrastructure.Persistence.Personnel;
using RESQ.Infrastructure.Persistence.System;
using RESQ.Infrastructure.Services;
using RESQ.Infrastructure.Services.Finance;
using RESQ.Infrastructure.Services.Identity;
using RESQ.Infrastructure.Services.Payments;
//using RESQ.Infrastructure.Services.Personnel;

namespace RESQ.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient();
        services.AddHttpClient("Goong", client =>
        {
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        services.AddDbContext<ResQDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("ResQDb"),
                x => x.UseNetTopologySuite()
            )
        );

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

        // Repositories
        services.AddScoped<IDepotRepository, DepotRepository>();
        services.AddScoped<IItemCategoryRepository, ItemCategoryRepository>();
        services.AddScoped<IDepotInventoryRepository, DepotInventoryRepository>();
        services.AddScoped<IInventoryLogRepository, InventoryLogRepository>();
        services.AddScoped<IInventoryMovementExportRepository, InventoryMovementExportRepository>();
        services.AddScoped<IOrganizationReliefRepository, OrganizationReliefRepository>();
        services.AddScoped<IReliefItemMetadataRepository, ReliefItemMetadataRepository>();
        services.AddScoped<IOrganizationMetadataRepository, OrganizationMetadataRepository>();
        services.AddScoped<IPurchasedInventoryRepository, PurchasedInventoryRepository>();
        services.AddScoped<ISupplyRequestRepository, SupplyRequestRepository>();
        services.AddScoped<IAssemblyPointRepository, AssemblyPointRepository>();
        services.AddScoped<IRescueTeamRepository, RescueTeamRepository>();
        services.AddScoped<IPersonnelQueryRepository, PersonnelQueryRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRescuerApplicationRepository, RescuerApplicationRepository>();
        services.AddScoped<IAbilityRepository, AbilityRepository>();
        services.AddScoped<IAbilityCategoryRepository, AbilityCategoryRepository>();
        services.AddScoped<IDocumentFileTypeRepository, DocumentFileTypeRepository>();
        services.AddScoped<IDocumentFileTypeCategoryRepository, DocumentFileTypeCategoryRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<ISosRequestRepository, SosRequestRepository>();
        services.AddScoped<ISosClusterRepository, SosClusterRepository>();
        services.AddScoped<ISosRuleEvaluationRepository, SosRuleEvaluationRepository>();
        services.AddScoped<ISosAiAnalysisRepository, SosAiAnalysisRepository>();
        services.AddScoped<IMissionAiSuggestionRepository, MissionAiSuggestionRepository>();
        services.AddScoped<IMissionRepository, MissionRepository>();
        services.AddScoped<IMissionActivityRepository, MissionActivityRepository>();
        services.AddScoped<IMissionTeamRepository, MissionTeamRepository>();
        services.AddScoped<ITeamIncidentRepository, TeamIncidentRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();

        // Finance Repositories
        services.AddScoped<IFundCampaignRepository, FundCampaignRepository>();
        services.AddScoped<IDonationRepository, DonationRepository>();
        services.AddScoped<IFundTransactionRepository, FundTransactionRepository>();
        services.AddScoped<IDepotFundAllocationRepository, DepotFundAllocationRepository>();
        services.AddScoped<IPaymentMethodRepository, PaymentMethodRepository>();

        // System Repositories
        services.AddScoped<IPromptRepository, PromptRepository>();
        services.AddScoped<IServiceZoneRepository, ServiceZoneRepository>();
        services.AddScoped<ISosPriorityRuleConfigRepository, SosPriorityRuleConfigRepository>();

        // Services
        services.AddScoped<IFirebaseService, FirebaseService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<ISosPriorityEvaluationService, SosPriorityEvaluationService>();
        services.AddScoped<ISosAiAnalysisService, SosAiAnalysisService>();
        services.AddScoped<IAiModelTestService, AiModelTestService>();
        services.AddScoped<IRescueMissionSuggestionService, RescueMissionSuggestionService>();
        services.AddScoped<IMissionContextService, MissionContextService>();
        services.AddScoped<IChatSupportAiService, ChatSupportAiService>();
        services.AddScoped<IUserPermissionResolver, UserPermissionResolver>();

        services.AddScoped<IGoongMapService, GoongMapService>();
        services.AddScoped<IExcelExportService, ExcelExportService>();
        
        // Domain Services
        services.AddScoped<IFundDistributionManager, FundDistributionManager>();
        services.AddScoped<IInventoryQueryService, InventoryQueryService>();

        // Payment Services
        services.AddScoped<PayOSService>();
        services.AddScoped<MomoPaymentService>();
        services.AddScoped<ZaloPayService>();
        services.AddScoped<IPaymentGatewayFactory, PaymentGatewayFactory>();
        services.AddScoped<IPaymentGatewayService>(sp => sp.GetRequiredService<PayOSService>());

        // Background Services
        services.AddSingleton<SosAiAnalysisQueue>();
        services.AddSingleton<ISosAiAnalysisQueue>(sp => sp.GetRequiredService<SosAiAnalysisQueue>());
        services.AddSingleton<ISosAiAnalysisQueueInternal>(sp => sp.GetRequiredService<SosAiAnalysisQueue>());
        services.AddHostedService<SosAiAnalysisBackgroundService>();
        services.AddHostedService<DonationExpirationBackgroundService>();
        //services.AddHostedService<TeamInvitationExpirationBackgroundService>();
        services.AddHostedService<UnverifiedUserCleanupBackgroundService>();

        return services;
    }
}
