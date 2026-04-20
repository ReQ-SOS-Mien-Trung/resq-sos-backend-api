using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Notifications;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Application.Services.Ai;
using RESQ.Domain.Entities.Finance.Services;
using RESQ.Domain.Entities.Logistics.Services;
using RESQ.Infrastructure.Options;
using RESQ.Infrastructure.Persistence.Base;
using RESQ.Infrastructure.Persistence.Context;
using RESQ.Infrastructure.Persistence.Seeding;
using RESQ.Infrastructure.Persistence.Emergency;
using RESQ.Infrastructure.Persistence.Finance;
using RESQ.Infrastructure.Persistence.Identity;
using RESQ.Infrastructure.Persistence.Logistics;
using RESQ.Infrastructure.Persistence.Notifications;
using RESQ.Infrastructure.Persistence.Operations;
using RESQ.Infrastructure.Persistence.Personnel;
using RESQ.Infrastructure.Persistence.System;
using RESQ.Infrastructure.Services;
using RESQ.Infrastructure.Services.Finance;
using RESQ.Infrastructure.Services.Identity;
using RESQ.Infrastructure.Services.Logistics;
using RESQ.Infrastructure.Services.Payments;
using RESQ.Infrastructure.Services.Ai;
using RESQ.Infrastructure.Services.Personnel;

namespace RESQ.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var bootstrapDatabaseOnStartup = configuration.GetValue<bool>("Database:BootstrapOnStartup");
        services.AddHttpClient();
        services.Configure<AiProvidersOptions>(configuration.GetSection("AiProviders"));
        var aiSecretsSection = configuration.GetSection("AiSecrets");
        services.Configure<AiSecretsOptions>(
            aiSecretsSection.Exists()
                ? aiSecretsSection
                : configuration.GetSection("PromptSecrets"));
        services.Configure<MissionSuggestionPipelineOptions>(configuration.GetSection("MissionSuggestionPipeline"));
        services.AddOptions<SeedDataOptions>();
        services.AddHttpClient("Goong", client =>
        {
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(
            configuration.GetConnectionString("ResQDb"));
        dataSourceBuilder.UseNetTopologySuite();
        dataSourceBuilder.EnableDynamicJson();
        var dataSource = dataSourceBuilder.Build();

        services.AddDbContext<ResQDbContext>(options =>
        {
            options.UseNpgsql(
                dataSource,
                x =>
                {
                    x.UseNetTopologySuite();
                    x.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: null);
                }
            );

            if (!bootstrapDatabaseOnStartup)
            {
                options.UseSeeding((context, _) =>
                {
                    SeedDatabase((ResQDbContext)context);
                });
                options.UseAsyncSeeding(async (context, _, cancellationToken) =>
                {
                    await SeedDatabaseAsync((ResQDbContext)context, cancellationToken);
                });
            }
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

        // Repositories
        services.AddScoped<IDepotRepository, DepotRepository>();
        services.AddScoped<IItemCategoryRepository, ItemCategoryRepository>();
        services.AddScoped<IDepotInventoryRepository, DepotInventoryRepository>();
        services.AddScoped<IDepotClosureRepository, DepotClosureRepository>();
        services.AddScoped<IDepotClosureExternalItemRepository, DepotClosureExternalItemRepository>();
        services.AddScoped<IDepotClosureTransferRepository, DepotClosureTransferRepository>();
        services.AddScoped<IUpcomingPickupActivityRepository, UpcomingPickupActivityRepository>();
        services.AddScoped<IReturnSupplyActivityRepository, ReturnSupplyActivityRepository>();
        services.AddScoped<IAdminActivityRepository, AdminActivityRepository>();
        services.AddScoped<IStockThresholdConfigRepository, StockThresholdConfigRepository>();
        services.AddScoped<IStockWarningBandConfigRepository, StockWarningBandConfigRepository>();
        services.AddScoped<IInventoryLogRepository, InventoryLogRepository>();
        services.AddScoped<IInventoryMovementExportRepository, InventoryMovementExportRepository>();
        services.AddScoped<IOrganizationReliefRepository, OrganizationReliefRepository>();
        services.AddScoped<IItemModelMetadataRepository, ItemModelMetadataRepository>();
        services.AddScoped<IOrganizationMetadataRepository, OrganizationMetadataRepository>();
        services.AddScoped<IPurchasedInventoryRepository, PurchasedInventoryRepository>();
        services.AddScoped<ISupplyRequestRepository, SupplyRequestRepository>();
        services.AddScoped<ISupplyRequestPriorityConfigRepository, SupplyRequestPriorityConfigRepository>();
        services.AddScoped<IAssemblyPointRepository, AssemblyPointRepository>();
        services.AddScoped<IAssemblyEventRepository, AssemblyEventRepository>();
        services.AddScoped<IRescueTeamRepository, RescueTeamRepository>();
        services.AddScoped<IPersonnelQueryRepository, PersonnelQueryRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRelativeProfileRepository, RelativeProfileRepository>();
        services.AddScoped<IRescuerScoreRepository, RescuerScoreRepository>();
        services.AddScoped<IRescuerApplicationRepository, RescuerApplicationRepository>();
        services.AddScoped<IAbilityRepository, AbilityRepository>();
        services.AddScoped<IAbilityCategoryRepository, AbilityCategoryRepository>();
        services.AddScoped<IDocumentFileTypeRepository, DocumentFileTypeRepository>();
        services.AddScoped<IDocumentFileTypeCategoryRepository, DocumentFileTypeCategoryRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<ISosRequestRepository, SosRequestRepository>();
        services.AddScoped<ISosRequestMapReadRepository, SosRequestRepository>();
        services.AddScoped<ISosRequestUpdateRepository, SosRequestUpdateRepository>();
        services.AddScoped<ISosRequestCompanionRepository, SosRequestCompanionRepository>();
        services.AddScoped<ISosClusterRepository, SosClusterRepository>();
        services.AddScoped<IClusterAiHistoryRepository, ClusterAiHistoryRepository>();
        services.AddScoped<ISosRuleEvaluationRepository, SosRuleEvaluationRepository>();
        services.AddScoped<ISosAiAnalysisRepository, SosAiAnalysisRepository>();
        services.AddScoped<IMissionAiSuggestionRepository, MissionAiSuggestionRepository>();
        services.AddScoped<IMissionRepository, MissionRepository>();
        services.AddScoped<IMissionActivityRepository, MissionActivityRepository>();
        services.AddScoped<IMissionActivitySyncMutationRepository, MissionActivitySyncMutationRepository>();
        services.AddScoped<IMissionTeamRepository, MissionTeamRepository>();
        services.AddScoped<IMissionTeamReportRepository, MissionTeamReportRepository>();
        services.AddScoped<ITeamIncidentRepository, TeamIncidentRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();

        // Notification Repository
        services.AddScoped<INotificationRepository, NotificationRepository>();

        // Finance Repositories
        services.AddScoped<IFundCampaignRepository, FundCampaignRepository>();
        services.AddScoped<IDonationRepository, DonationRepository>();
        services.AddScoped<IFundTransactionRepository, FundTransactionRepository>();
        services.AddScoped<ICampaignDisbursementRepository, CampaignDisbursementRepository>();
        services.AddScoped<IFundingRequestRepository, FundingRequestRepository>();
        services.AddScoped<IDepotFundRepository, DepotFundRepository>();
        services.AddScoped<ISystemFundRepository, SystemFundRepository>();

        // System Repositories
        services.AddScoped<IAiConfigRepository, AiConfigRepository>();
        services.AddScoped<IPromptRepository, PromptRepository>();
        services.AddScoped<IRescuerScoreVisibilityConfigRepository, RescuerScoreVisibilityConfigRepository>();
        services.AddScoped<IServiceZoneRepository, ServiceZoneRepository>();
        services.AddScoped<IServiceZoneSummaryRepository, ServiceZoneSummaryRepository>();
        services.AddScoped<ISosClusterGroupingConfigRepository, SosClusterGroupingConfigRepository>();
        services.AddScoped<IRescueTeamRadiusConfigRepository, RescueTeamRadiusConfigRepository>();
        services.AddScoped<ISosPriorityRuleConfigRepository, SosPriorityRuleConfigRepository>();
        services.AddScoped<ICheckInRadiusConfigRepository, CheckInRadiusConfigRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();

        // Services
        services.AddSingleton<IAiSecretProtector, AiSecretProtector>();
        services.AddSingleton<IPromptSecretProtector>(sp => (IPromptSecretProtector)sp.GetRequiredService<IAiSecretProtector>());
        services.AddScoped<IAiPromptExecutionSettingsResolver, AiPromptExecutionSettingsResolver>();
        services.AddScoped<IAiProviderClient, GeminiAiProviderClient>();
        services.AddScoped<IAiProviderClient, OpenRouterAiProviderClient>();
        services.AddScoped<IAiProviderClientFactory, AiProviderClientFactory>();
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
        services.AddScoped<IManagerDepotAccessService, ManagerDepotAccessService>();
        services.AddScoped<IDepotRealtimeOutboxAdminService, DepotRealtimeOutboxAdminService>();

        services.AddScoped<IGoongMapService, GoongMapService>();
        services.AddScoped<IExcelExportService, ExcelExportService>();
        services.AddScoped<IFundingRequestExcelParser, FundingRequestExcelParser>();
        
        // Domain Services
        services.AddScoped<IFundDistributionManager, FundDistributionManager>();
        services.AddScoped<IDepotFundDrainService, DepotFundDrainService>();
        services.AddScoped<IInventoryQueryService, InventoryQueryService>();
        services.AddScoped<IStockThresholdResolver, StockThresholdResolver>();
        services.AddScoped<IStockWarningEvaluatorService, StockWarningEvaluatorService>();
        services.AddScoped<DemoSeedValidator>();
        services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();

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
        services.AddHostedService<AiConfigBackfillHostedService>();
        services.AddHostedService<DonationExpirationBackgroundService>();
        //services.AddHostedService<TeamInvitationExpirationBackgroundService>();
        services.AddHostedService<UnverifiedUserCleanupBackgroundService>();
        services.AddHostedService<CampaignDeadlineBackgroundService>();
        services.AddHostedService<DepotRealtimeOutboxDispatcherBackgroundService>();
        services.AddHostedService<DepotRealtimeDeadLetterRetryBackgroundService>();
        services.AddHostedService<SupplyRequestDeadlineBackgroundService>();
        services.AddHostedService<InventoryItemModelAlertBackgroundService>();
        services.AddHostedService<AssemblyCheckInDeadlineBackgroundService>();
        return services;
    }

    private static void SeedDatabase(ResQDbContext context)
    {
        SeedDatabaseAsync(context, CancellationToken.None).GetAwaiter().GetResult();
    }

    private static async Task SeedDatabaseAsync(ResQDbContext context, CancellationToken cancellationToken)
    {
        var seeder = new DatabaseSeeder(
            context,
            Microsoft.Extensions.Options.Options.Create(new SeedDataOptions()),
            new DemoSeedValidator(),
            NullLogger<DatabaseSeeder>.Instance);

        await seeder.SeedAsync(cancellationToken);
    }

    /// <summary>Gọi seeder độc lập, ngoài EF execution strategy của EnsureCreated/Migrate.</summary>
    public static async Task RunSeedAsync(ResQDbContext context, CancellationToken cancellationToken = default)
    {
        await SeedDatabaseAsync(context, cancellationToken);
    }
}
