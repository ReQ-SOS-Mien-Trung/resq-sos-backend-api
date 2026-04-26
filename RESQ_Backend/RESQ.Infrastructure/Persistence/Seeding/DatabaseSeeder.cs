using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using Npgsql;
using RESQ.Application.Common.Constants;
using RESQ.Domain.Enum.Emergency;
using RESQ.Domain.Enum.Finance;
using RESQ.Domain.Enum.Logistics;
using RESQ.Infrastructure.Entities.Emergency;
using RESQ.Infrastructure.Entities.Finance;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Entities.Operations;
using RESQ.Infrastructure.Entities.Personnel;
using RESQ.Infrastructure.Entities.System;
using RESQ.Application.Common.Models;
using RESQ.Application.Services;
using RESQ.Infrastructure.Persistence.Context;
using LogisticsTargetGroup = RESQ.Infrastructure.Entities.Logistics.TargetGroup;

namespace RESQ.Infrastructure.Persistence.Seeding;

public sealed class DatabaseSeeder : IDatabaseSeeder
{
    private const string MarkerName = "demo-seed-v5-2026-04-25";
    private const int TotalRescuerCount = 200;
    private const int RecentRescuerCount = 20;
    private const int UnassignedRescuerCount = 40;
    private const int EligibleAssignedRescuerCount = 120;
    private const int HueStadiumUnclusteredSosCount = 10;
    private const int HueStadiumSosClusterCount = 11;
    private const int HueStadiumSosRequestCount = 20;
    private const int HueStadiumCheckedInStandbyRescuerCount = 10;
    private const int HueStadiumReserveTeamCount = 2;
    private const int HueStadiumReserveTeamMemberCount = 6;
    private const string HueStadiumReserveTeamCodePrefix = "RT-HUE-TD-AV";
    private static readonly string[] DepotClosureTestDepotNames =
    [
        "Kho cứu trợ Đại học Phú Yên",
        "Ga đường sắt Sài Gòn"
    ];
    private static readonly string[] HueDepotExcludedItemNames =
    [
        "Pin dự phòng 10000mAh",
        "Bộ đèn pin đội đầu"
    ];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] ReferenceIdentityTables =
    [
        "notifications",
        "ai_configs",
        "prompts",
        "roles",
        "permissions",
        "document_file_type_categories",
        "document_file_types",
        "service_zones",
        "target_groups",
        "inventory_stock_threshold_configs",
        "stock_warning_band_config"
    ];
    private readonly ResQDbContext _db;
    private readonly SeedDataOptions _options;
    private readonly DemoSeedValidator _validator;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        ResQDbContext db,
        IOptions<SeedDataOptions> options,
        DemoSeedValidator validator,
        ILogger<DatabaseSeeder> logger)
    {
        _db = db;
        _options = options.Value;
        _validator = validator;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.IsDemoProfile)
        {
            return;
        }

        await EnsurePostGisExtensionAsync(cancellationToken);
        await SeedReferenceDataAsync(cancellationToken);
        await SeedAiSuggestionsAsync(cancellationToken);

        if (await _db.SystemMigrationAudits.AnyAsync(a => a.MigrationName == MarkerName, cancellationToken))
        {
            _logger.LogInformation("Runtime demo seed skipped because marker {MarkerName} already exists.", MarkerName);
            return;
        }

        if (await HasOperationalDataAsync(cancellationToken))
        {
            _db.SystemMigrationAudits.Add(new SystemMigrationAudit
            {
                MigrationName = MarkerName,
                AppliedAt = DateTime.UtcNow,
                Notes = "Runtime demo seed skipped because operational data already existed."
            });
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogWarning("Runtime demo seed marker was added without seeding because operational data already exists.");
            return;
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            IDbContextTransaction? transaction = null;
            var ownsTransaction = false;
            try
            {
                if (_db.Database.IsRelational() && _db.Database.CurrentTransaction is null)
                {
                    transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
                    ownsTransaction = true;
                }

                var seed = CreateContext();

                await SeedStaticConfigAsync(seed, cancellationToken);
                await SeedIdentityAsync(seed, cancellationToken);
                await SeedPersonnelAsync(seed, cancellationToken);
                await SeedLogisticsCatalogAsync(seed, cancellationToken);
                await SeedDepotsAndInventoryAsync(seed, cancellationToken);
                await SeedEmergencyAsync(seed, cancellationToken);
                await SeedMissionsAsync(seed, cancellationToken);
                await SeedAiSuggestionsAsync(cancellationToken);
                await SeedChatAsync(seed, cancellationToken);
                await SeedSupplyRequestsAsync(seed, cancellationToken);
                await SeedFinanceAsync(seed, cancellationToken);
                await SeedAuditAndHistoryAsync(seed, cancellationToken);

                var validationErrors = await _validator.ValidateAsync(_db, cancellationToken);
                if (validationErrors.Count > 0)
                {
                    var message = "Runtime demo seed validation failed: " + string.Join(" | ", validationErrors);
                    if (_options.FailOnValidationError)
                    {
                        throw new InvalidOperationException(message);
                    }

                    _logger.LogWarning("{Message}", message);
                }

                _db.SystemMigrationAudits.Add(new SystemMigrationAudit
                {
                    MigrationName = MarkerName,
                    AppliedAt = DateTime.UtcNow,
                    Notes = $"SeedData profile={_options.Profile}; anchor={_options.AnchorDate:yyyy-MM-dd}; randomSeed={_options.RandomSeed}"
                });
                await _db.SaveChangesAsync(cancellationToken);

                if (ownsTransaction && transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                _logger.LogInformation("Runtime demo seed completed with marker {MarkerName}.", MarkerName);
            }
            finally
            {
                if (ownsTransaction && transaction is not null)
                {
                    await transaction.DisposeAsync();
                }
            }
        });
    }

    private async Task EnsurePostGisExtensionAsync(CancellationToken cancellationToken)
    {
        if (!_db.Database.IsRelational())
        {
            return;
        }

        var providerName = _db.Database.ProviderName;
        if (string.IsNullOrWhiteSpace(providerName)
            || !providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Dùng ADO.NET trực tiếp để tránh NpgsqlRetryingExecutionStrategy conflict
        var connection = _db.Database.GetDbConnection();
        if (connection is not NpgsqlConnection npgsqlConn)
            return;

        var wasClosed = npgsqlConn.State == ConnectionState.Closed;
        if (wasClosed)
            await npgsqlConn.OpenAsync(cancellationToken);

        try
        {
            // Kiểm tra PostGIS đã tồn tại chưa
            bool hasPostGis;
            await using (var checkCmd = npgsqlConn.CreateCommand())
            {
                checkCmd.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'postgis');";
                var result = await checkCmd.ExecuteScalarAsync(cancellationToken);
                hasPostGis = result is true;
            }

            if (hasPostGis)
            {
                await npgsqlConn.ReloadTypesAsync();
                return;
            }

            // Thử tạo extension
            try
            {
                await using var createCmd = npgsqlConn.CreateCommand();
                createCmd.CommandText = "CREATE EXTENSION IF NOT EXISTS postgis;";
                await createCmd.ExecuteNonQueryAsync(cancellationToken);
                await npgsqlConn.ReloadTypesAsync();
            }
            catch (Exception ex)
            {
                // Neon.tech / managed PostgreSQL thường đã cài sẵn PostGIS nhưng không cho phép
                // CREATE EXTENSION (superuser only). Log warning thay vì crash startup.
                _logger.LogWarning(ex,
                    "Could not create PostGIS extension (may require superuser). " +
                    "Ensure PostGIS is pre-installed on the server if geography columns are used.");
            }
        }
        finally
        {
            if (wasClosed)
                await npgsqlConn.CloseAsync();
        }
    }

    private async Task ReloadPostgresTypesAsync(CancellationToken cancellationToken)
    {
        var connection = _db.Database.GetDbConnection();
        if (connection is not NpgsqlConnection npgsqlConnection)
        {
            return;
        }

        var wasClosed = npgsqlConnection.State == ConnectionState.Closed;
        if (wasClosed)
        {
            await npgsqlConnection.OpenAsync(cancellationToken);
        }

        await npgsqlConnection.ReloadTypesAsync();

        if (wasClosed)
        {
            await npgsqlConnection.CloseAsync();
        }
    }

    private async Task SeedReferenceDataAsync(CancellationToken cancellationToken)
    {
        var referenceTimestamp = _options.AnchorDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        await SeedCoreReferenceDataAsync(referenceTimestamp, cancellationToken);
        await SeedDependentReferenceDataAsync(referenceTimestamp, cancellationToken);
        await ResetReferenceIdentitySequencesAsync(cancellationToken);
    }

    private async Task SeedCoreReferenceDataAsync(DateTime referenceTimestamp, CancellationToken cancellationToken)
    {
        var existingNotificationIds = await _db.Notifications
            .Select(notification => notification.Id)
            .ToListAsync(cancellationToken);
        var existingNotificationIdSet = existingNotificationIds.ToHashSet();
        _db.Notifications.AddRange(SystemSeeder.CreateNotifications()
            .Where(notification => !existingNotificationIdSet.Contains(notification.Id)));

        var existingAiConfigIds = await _db.AiConfigs
            .Select(config => config.Id)
            .ToListAsync(cancellationToken);
        var existingAiConfigIdSet = existingAiConfigIds.ToHashSet();
        _db.AiConfigs.AddRange(SystemSeeder.CreateAiConfigs()
            .Where(config => !existingAiConfigIdSet.Contains(config.Id)));

        var existingPromptIds = await _db.Prompts
            .Select(prompt => prompt.Id)
            .ToListAsync(cancellationToken);
        var existingPromptIdSet = existingPromptIds.ToHashSet();
        _db.Prompts.AddRange(SystemSeeder.CreatePrompts()
            .Where(prompt => !existingPromptIdSet.Contains(prompt.Id)));

        var existingRoleIds = await _db.Roles
            .Select(role => role.Id)
            .ToListAsync(cancellationToken);
        var existingRoleIdSet = existingRoleIds.ToHashSet();
        var roles = new[]
        {
            new Role { Id = RoleConstants.Admin, Name = "Admin" },
            new Role { Id = RoleConstants.Coordinator, Name = "Coordinator" },
            new Role { Id = RoleConstants.Rescuer, Name = "Rescuer" },
            new Role { Id = RoleConstants.Manager, Name = "Manager" },
            new Role { Id = RoleConstants.Victim, Name = "Victim" }
        };
        _db.Roles.AddRange(roles.Where(role => !existingRoleIdSet.Contains(role.Id)));

        var existingPermissionIds = await _db.Permissions
            .Select(permission => permission.Id)
            .ToListAsync(cancellationToken);
        var existingPermissionIdSet = existingPermissionIds.ToHashSet();
        _db.Permissions.AddRange(PermissionSeeder.CreatePermissions()
            .Where(permission => !existingPermissionIdSet.Contains(permission.Id)));

        var existingDocumentCategoryIds = await _db.DocumentFileTypeCategories
            .Select(category => category.Id)
            .ToListAsync(cancellationToken);
        var existingDocumentCategoryIdSet = existingDocumentCategoryIds.ToHashSet();
        var documentCategories = new[]
        {
            new DocumentFileTypeCategory { Id = 1, Code = "RESCUE", Description = "Tài liệu danh mục cứu hộ" },
            new DocumentFileTypeCategory { Id = 2, Code = "MEDICAL", Description = "Tài liệu danh mục y tế" },
            new DocumentFileTypeCategory { Id = 3, Code = "TRANSPORTATION", Description = "Tài liệu danh mục vận chuyển" },
            new DocumentFileTypeCategory { Id = 4, Code = "OTHER", Description = "Tài liệu danh mục khác" }
        };
        _db.DocumentFileTypeCategories.AddRange(documentCategories
            .Where(category => !existingDocumentCategoryIdSet.Contains(category.Id)));

        var existingTargetGroupIds = await _db.TargetGroups
            .Select(targetGroup => targetGroup.Id)
            .ToListAsync(cancellationToken);
        var existingTargetGroupIdSet = existingTargetGroupIds.ToHashSet();
        var targetGroups = new[]
        {
            new LogisticsTargetGroup { Id = 1, Name = "Children" },
            new LogisticsTargetGroup { Id = 2, Name = "Elderly" },
            new LogisticsTargetGroup { Id = 3, Name = "Pregnant" },
            new LogisticsTargetGroup { Id = 4, Name = "Adult" },
            new LogisticsTargetGroup { Id = 5, Name = "Rescuer" }
        };
        _db.TargetGroups.AddRange(targetGroups
            .Where(targetGroup => !existingTargetGroupIdSet.Contains(targetGroup.Id)));

        if (!await _db.InventoryStockThresholdConfigs.AnyAsync(config => config.Id == 1, cancellationToken))
        {
            _db.InventoryStockThresholdConfigs.Add(new InventoryStockThresholdConfig
            {
                Id = 1,
                ScopeType = "GLOBAL",
                MinimumThreshold = 100,
                IsActive = true,
                UpdatedBy = null,
                UpdatedAt = referenceTimestamp,
                RowVersion = 1
            });
        }

        if (!await _db.StockWarningBandConfigs.AnyAsync(config => config.Id == 1, cancellationToken))
        {
            _db.StockWarningBandConfigs.Add(new StockWarningBandConfig
            {
                Id = 1,
                BandsJson = "[{\"name\":\"CRITICAL\",\"from\":0.0,\"to\":0.25},{\"name\":\"MEDIUM\",\"from\":0.25,\"to\":0.5},{\"name\":\"LOW\",\"from\":0.5,\"to\":0.8},{\"name\":\"OK\",\"from\":0.8,\"to\":null}]",
                UpdatedBy = null,
                UpdatedAt = referenceTimestamp
            });
        }

        await SeedServiceZonesAsync(referenceTimestamp, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedDependentReferenceDataAsync(DateTime referenceTimestamp, CancellationToken cancellationToken)
    {
        var existingRolePermissionKeys = await _db.RolePermissions
            .Select(rolePermission => new { rolePermission.RoleId, rolePermission.ClaimId })
            .ToListAsync(cancellationToken);
        var existingRolePermissionKeySet = existingRolePermissionKeys
            .Select(rolePermission => (rolePermission.RoleId, rolePermission.ClaimId))
            .ToHashSet();
        _db.RolePermissions.AddRange(PermissionSeeder.CreateRolePermissions()
            .Where(rolePermission => !existingRolePermissionKeySet.Contains((rolePermission.RoleId, rolePermission.ClaimId))));

        var existingDocumentFileTypeIds = await _db.DocumentFileTypes
            .Select(fileType => fileType.Id)
            .ToListAsync(cancellationToken);
        var existingDocumentFileTypeIdSet = existingDocumentFileTypeIds.ToHashSet();
        _db.DocumentFileTypes.AddRange(DocumentFileTypes(referenceTimestamp)
            .Where(fileType => !existingDocumentFileTypeIdSet.Contains(fileType.Id)));

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedServiceZonesAsync(DateTime timestamp, CancellationToken cancellationToken)
    {
        var existingServiceZoneKeys = await _db.ServiceZones
            .Select(zone => new { zone.Id, zone.Name })
            .ToListAsync(cancellationToken);
        var existingServiceZoneIds = existingServiceZoneKeys
            .Select(zone => zone.Id)
            .ToHashSet();
        var existingServiceZoneNames = existingServiceZoneKeys
            .Select(zone => zone.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var zone in ServiceZones(timestamp))
        {
            if (existingServiceZoneIds.Contains(zone.Id)
                || existingServiceZoneNames.Contains(zone.Name))
            {
                continue;
            }

            _db.ServiceZones.Add(zone);
        }
    }

    private async Task ResetReferenceIdentitySequencesAsync(CancellationToken cancellationToken)
    {
        await ResetIdentitySequencesAsync(ReferenceIdentityTables, cancellationToken);
    }

    private async Task ResetIdentitySequencesAsync(IEnumerable<string> tableNames, CancellationToken cancellationToken)
    {
        if (!string.Equals(
            _db.Database.ProviderName,
            "Npgsql.EntityFrameworkCore.PostgreSQL",
            StringComparison.Ordinal))
        {
            return;
        }

        foreach (var tableName in tableNames)
        {
            var resetSequenceSql =
                $"""
                SELECT setval(
                    pg_get_serial_sequence('{tableName}', 'id'),
                    COALESCE((SELECT MAX(id) FROM {tableName}), 1),
                    true)
                WHERE pg_get_serial_sequence('{tableName}', 'id') IS NOT NULL;
                """;

            await _db.Database.ExecuteSqlRawAsync(
                resetSequenceSql,
                cancellationToken);
        }
    }

    private async Task SeedAiSuggestionsAsync(CancellationToken cancellationToken)
    {
        var clusterIds = await _db.SosClusters
            .OrderBy(cluster => cluster.Id)
            .Take(2)
            .Select(cluster => cluster.Id)
            .ToArrayAsync(cancellationToken);
        var adoptedRescueTeamId = await _db.RescueTeams
            .OrderBy(team => team.Id)
            .Select(team => (int?)team.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (clusterIds.Length == 0)
        {
            return;
        }

        var existingClusterAnalysisIds = await _db.ClusterAiAnalyses
            .Select(analysis => analysis.Id)
            .ToListAsync(cancellationToken);
        var existingClusterAnalysisIdSet = existingClusterAnalysisIds.ToHashSet();
        var clusterAnalysisTemplates = AiAnalysisSeeder.CreateClusterAiAnalyses();
        for (var index = 0; index < Math.Min(clusterAnalysisTemplates.Count, clusterIds.Length); index++)
        {
            var template = clusterAnalysisTemplates[index];
            if (existingClusterAnalysisIdSet.Contains(template.Id))
            {
                continue;
            }

            _db.ClusterAiAnalyses.Add(new ClusterAiAnalysis
            {
                Id = template.Id,
                ClusterId = clusterIds[index],
                ModelName = template.ModelName,
                ModelVersion = template.ModelVersion,
                AnalysisType = template.AnalysisType,
                SuggestedSeverityLevel = template.SuggestedSeverityLevel,
                SuggestedMissionTypes = template.SuggestedMissionTypes,
                SuggestionScope = template.SuggestionScope,
                Metadata = template.Metadata,
                CreatedAt = template.CreatedAt,
                AdoptedAt = template.AdoptedAt
            });
        }

        var existingActivitySuggestionIds = await _db.ActivityAiSuggestions
            .Select(suggestion => suggestion.Id)
            .ToListAsync(cancellationToken);
        var existingActivitySuggestionIdSet = existingActivitySuggestionIds.ToHashSet();
        var activitySuggestionTemplates = AiAnalysisSeeder.CreateActivityAiSuggestions();
        for (var index = 0; index < Math.Min(activitySuggestionTemplates.Count, clusterIds.Length); index++)
        {
            var template = activitySuggestionTemplates[index];
            if (existingActivitySuggestionIdSet.Contains(template.Id))
            {
                continue;
            }

            _db.ActivityAiSuggestions.Add(new ActivityAiSuggestion
            {
                Id = template.Id,
                ClusterId = clusterIds[index],
                ParentMissionSuggestionId = template.ParentMissionSuggestionId,
                AdoptedActivityId = template.AdoptedActivityId,
                ModelName = template.ModelName,
                ModelVersion = template.ModelVersion,
                ActivityType = template.ActivityType,
                SuggestionPhase = template.SuggestionPhase,
                SuggestedActivities = template.SuggestedActivities,
                SuggestionScope = template.SuggestionScope,
                CreatedAt = template.CreatedAt,
                AdoptedAt = template.AdoptedAt
            });
        }

        var existingRescueTeamSuggestionIds = await _db.RescueTeamAiSuggestions
            .Select(suggestion => suggestion.Id)
            .ToListAsync(cancellationToken);
        var existingRescueTeamSuggestionIdSet = existingRescueTeamSuggestionIds.ToHashSet();
        var rescueTeamSuggestionTemplates = AiAnalysisSeeder.CreateRescueTeamAiSuggestions();
        for (var index = 0; index < Math.Min(rescueTeamSuggestionTemplates.Count, clusterIds.Length); index++)
        {
            var template = rescueTeamSuggestionTemplates[index];
            if (existingRescueTeamSuggestionIdSet.Contains(template.Id))
            {
                continue;
            }

            _db.RescueTeamAiSuggestions.Add(new RescueTeamAiSuggestion
            {
                Id = template.Id,
                ClusterId = clusterIds[index],
                AdoptedRescueTeamId = adoptedRescueTeamId,
                ModelName = template.ModelName,
                ModelVersion = template.ModelVersion,
                AnalysisType = template.AnalysisType,
                SuggestedMembers = template.SuggestedMembers,
                SuggestionScope = template.SuggestionScope,
                CreatedAt = template.CreatedAt,
                AdoptedAt = template.AdoptedAt
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        await ResetIdentitySequencesAsync(
            [
                "cluster_ai_analysis",
                "activity_ai_suggestions",
                "rescue_team_ai_suggestions"
            ],
            cancellationToken);
    }

    private async Task<bool> HasOperationalDataAsync(CancellationToken cancellationToken)
    {
        return await _db.Users.AnyAsync(cancellationToken)
            || await _db.SosRequests.AnyAsync(cancellationToken)
            || await _db.Missions.AnyAsync(cancellationToken)
            || await _db.SupplyInventories.AnyAsync(cancellationToken)
            || await _db.FundCampaigns.AnyAsync(cancellationToken);
    }

    private DemoSeedContext CreateContext()
    {
        var anchorLocal = _options.AnchorDate.ToDateTime(TimeOnly.MinValue);
        var anchorUtc = VnToUtc(anchorLocal.AddDays(1).AddTicks(-1));
        var startUtc = VnToUtc(_options.AnchorDate.AddYears(-3).ToDateTime(TimeOnly.MinValue));

        return new DemoSeedContext
        {
            Options = _options,
            Random = new Random(_options.RandomSeed),
            AnchorUtc = anchorUtc,
            StartUtc = startUtc
        };
    }

    private async Task SeedStaticConfigAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        if (!await _db.AbilityCategories.AnyAsync(cancellationToken))
        {
            var categories = new[]
            {
                new AbilityCategory
                {
                    Code = "RESCUE",
                    Description = "Kỹ năng cứu hộ",
                    Subgroups =
                    {
                        new AbilitySubgroup
                        {
                            Code = "WATER_SKILLS",
                            Description = "Kỹ năng bơi lội",
                            Abilities =
                            {
                                new Ability { Code = "BASIC_SWIMMING", Description = "Bơi cơ bản" },
                                new Ability { Code = "ADVANCED_SWIMMING", Description = "Bơi thành thạo" },
                                new Ability { Code = "WATER_RESCUE", Description = "Cứu hộ dưới nước" },
                                new Ability { Code = "DEEP_WATER_MOVEMENT", Description = "Di chuyển trong nước ngập sâu" },
                                new Ability { Code = "RAPID_WATER_MOVEMENT", Description = "Di chuyển trong dòng nước chảy xiết" },
                                new Ability { Code = "BASIC_DIVING", Description = "Lặn cơ bản" },
                                new Ability { Code = "FLOOD_ESCAPE", Description = "Thoát hiểm trong môi trường ngập nước" }
                            }
                        },
                        new AbilitySubgroup
                        {
                            Code = "LIFESAVING_SKILLS",
                            Description = "Kỹ năng cứu người",
                            Abilities =
                            {
                                new Ability { Code = "FLOODED_HOUSE_RESCUE", Description = "Cứu người bị mắc kẹt trong nhà ngập" },
                                new Ability { Code = "ROOFTOP_RESCUE", Description = "Cứu người bị mắc kẹt trên mái nhà" },
                                new Ability { Code = "VEHICLE_RESCUE", Description = "Cứu người bị kẹt trong phương tiện (xe, ghe)" },
                                new Ability { Code = "ROPE_RESCUE", Description = "Sử dụng dây thừng cứu hộ" },
                                new Ability { Code = "LIFE_JACKET_USE", Description = "Sử dụng áo phao, phao cứu sinh" }
                            }
                        },
                        new AbilitySubgroup
                        {
                            Code = "HARSH_ENVIRONMENT_RESCUE",
                            Description = "Cứu hộ trong điều kiện khắc nghiệt",
                            Abilities =
                            {
                                new Ability { Code = "NIGHT_RESCUE", Description = "Cứu hộ ban đêm / tầm nhìn kém" },
                                new Ability { Code = "STORM_RESCUE", Description = "Cứu hộ trong mưa lớn / bão" },
                                new Ability { Code = "DEBRIS_RESCUE", Description = "Cứu hộ tại khu vực đổ nát" },
                                new Ability { Code = "HAZARDOUS_RESCUE", Description = "Cứu hộ trong môi trường nguy hiểm" }
                            }
                        }
                    }
                },
                new AbilityCategory
                {
                    Code = "MEDICAL",
                    Description = "Kỹ năng y tế",
                    Subgroups =
                    {
                        new AbilitySubgroup
                        {
                            Code = "PROFESSIONAL_MEDICAL",
                            Description = "Y tế chuyên môn",
                            Abilities =
                            {
                                new Ability { Code = "MEDICAL_STAFF", Description = "Nhân viên y tế" },
                                new Ability { Code = "NURSE", Description = "Y tá" },
                                new Ability { Code = "DOCTOR", Description = "Bác sĩ" },
                                new Ability { Code = "PREHOSPITAL_EMERGENCY", Description = "Cấp cứu tiền viện" }
                            }
                        },
                        new AbilitySubgroup
                        {
                            Code = "BASIC_FIRST_AID",
                            Description = "Sơ cứu cơ bản",
                            Abilities =
                            {
                                new Ability { Code = "BASIC_FIRST_AID", Description = "Sơ cứu cơ bản" },
                                new Ability { Code = "OPEN_WOUND_CARE", Description = "Sơ cứu vết thương hở" },
                                new Ability { Code = "BLEEDING_CONTROL", Description = "Cầm máu" },
                                new Ability { Code = "WOUND_BANDAGING", Description = "Băng bó vết thương" },
                                new Ability { Code = "MINOR_INJURY_CARE", Description = "Xử lý trầy xước, chấn thương nhẹ" },
                                new Ability { Code = "MINOR_BURN_CARE", Description = "Xử lý bỏng nhẹ" }
                            }
                        },
                        new AbilitySubgroup
                        {
                            Code = "EMERGENCY_CARE",
                            Description = "Cấp cứu",
                            Abilities =
                            {
                                new Ability { Code = "CPR", Description = "Hồi sức tim phổi (CPR)" },
                                new Ability { Code = "DROWNING_RESPONSE", Description = "Xử lý đuối nước" },
                                new Ability { Code = "SHOCK_TREATMENT", Description = "Xử lý sốc" },
                                new Ability { Code = "HYPOTHERMIA_TREATMENT", Description = "Xử lý hạ thân nhiệt" },
                                new Ability { Code = "VITAL_SIGNS_MONITORING", Description = "Theo dõi dấu hiệu sinh tồn" },
                                new Ability { Code = "VICTIM_ASSESSMENT", Description = "Đánh giá mức độ nguy kịch nạn nhân" }
                            }
                        },
                        new AbilitySubgroup
                        {
                            Code = "TRAUMA_CARE",
                            Description = "Chấn thương",
                            Abilities =
                            {
                                new Ability { Code = "FRACTURE_IMMOBILIZATION", Description = "Cố định gãy xương tạm thời" },
                                new Ability { Code = "SPINAL_INJURY_CARE", Description = "Xử lý chấn thương cột sống (cơ bản)" },
                                new Ability { Code = "SAFE_PATIENT_TRANSPORT", Description = "Vận chuyển người bị thương an toàn" }
                            }
                        }
                    }
                },
                new AbilityCategory
                {
                    Code = "TRANSPORTATION",
                    Description = "Kỹ năng vận chuyển",
                    Subgroups =
                    {
                        new AbilitySubgroup
                        {
                            Code = "LAND_VEHICLES",
                            Description = "Lái xe cơ giới",
                            Abilities =
                            {
                                new Ability { Code = "MOTORCYCLE_DRIVING", Description = "Lái xe máy" },
                                new Ability { Code = "MOTORCYCLE_FLOOD_DRIVING", Description = "Lái xe máy trong điều kiện ngập nước" },
                                new Ability { Code = "CAR_DRIVING", Description = "Lái ô tô" },
                                new Ability { Code = "OFFROAD_DRIVING", Description = "Lái ô tô địa hình" }
                            }
                        },
                        new AbilitySubgroup
                        {
                            Code = "WATER_VEHICLES",
                            Description = "Lái phương tiện thủy",
                            Abilities =
                            {
                                new Ability { Code = "ROWBOAT_DRIVING", Description = "Lái ghe" },
                                new Ability { Code = "DINGHY_DRIVING", Description = "Lái xuồng" },
                                new Ability { Code = "SPEEDBOAT_DRIVING", Description = "Lái ca nô" }
                            }
                        },
                        new AbilitySubgroup
                        {
                            Code = "SPECIALIZED_DRIVING",
                            Description = "Kỹ năng điều khiển đặc biệt",
                            Abilities =
                            {
                                new Ability { Code = "NIGHT_VEHICLE_OPERATION", Description = "Điều khiển phương tiện ban đêm" },
                                new Ability { Code = "RAIN_VEHICLE_OPERATION", Description = "Điều khiển phương tiện trong mưa lớn" }
                            }
                        },
                        new AbilitySubgroup
                        {
                            Code = "TRANSPORT_OPERATIONS",
                            Description = "Vận chuyển",
                            Abilities =
                            {
                                new Ability { Code = "VICTIM_TRANSPORT", Description = "Vận chuyển nạn nhân" },
                                new Ability { Code = "RELIEF_GOODS_TRANSPORT", Description = "Vận chuyển hàng cứu trợ" },
                                new Ability { Code = "HEAVY_CARGO_TRANSPORT", Description = "Vận chuyển hàng nặng" }
                            }
                        }
                    }
                },
                new AbilityCategory
                {
                    Code = "EXPERIENCE",
                    Description = "Kinh nghiệm thực tiễn",
                    Subgroups =
                    {
                        new AbilitySubgroup
                        {
                            Code = "FIELD_EXPERIENCE",
                            Description = "Kinh nghiệm thực tế",
                            Abilities =
                            {
                                new Ability { Code = "DISASTER_RELIEF_EXPERIENCE", Description = "Đã tham gia cứu trợ thiên tai" },
                                new Ability { Code = "FLOOD_RESCUE_EXPERIENCE", Description = "Kinh nghiệm cứu hộ lũ lụt" },
                                new Ability { Code = "COMMUNITY_RESCUE_EXPERIENCE", Description = "Kinh nghiệm cứu hộ cộng đồng" }
                            }
                        },
                        new AbilitySubgroup
                        {
                            Code = "ORGANIZATIONAL_MEMBERSHIP",
                            Description = "Tổ chức",
                            Abilities =
                            {
                                new Ability { Code = "LOCAL_RESCUE_TEAM_MEMBER", Description = "Thành viên đội cứu hộ địa phương" },
                                new Ability { Code = "VOLUNTEER_ORG_MEMBER", Description = "Thành viên tổ chức thiện nguyện" }
                            }
                        }
                    }
                }
            };

            _db.AbilityCategories.AddRange(categories);
        }

        if (!await _db.CheckInRadiusConfigs.AnyAsync(cancellationToken))
        {
            _db.CheckInRadiusConfigs.Add(new CheckInRadiusConfig { MaxRadiusMeters = 150, UpdatedAt = seed.AnchorUtc });
        }

        if (!await _db.RescueTeamRadiusConfigs.AnyAsync(cancellationToken))
        {
            _db.RescueTeamRadiusConfigs.Add(new RescueTeamRadiusConfig { MaxRadiusKm = 10, UpdatedAt = seed.AnchorUtc });
        }

        if (!await _db.RescuerScoreVisibilityConfigs.AnyAsync(cancellationToken))
        {
            _db.RescuerScoreVisibilityConfigs.Add(new RescuerScoreVisibilityConfig { MinimumEvaluationCount = 3, UpdatedAt = seed.AnchorUtc });
        }

        if (!await _db.SosClusterGroupingConfigs.AnyAsync(cancellationToken))
        {
            _db.SosClusterGroupingConfigs.Add(new SosClusterGroupingConfig { MaximumDistanceKm = 4.5, UpdatedAt = seed.AnchorUtc });
        }

        if (!await _db.SupplyRequestPriorityConfigs.AnyAsync(cancellationToken))
        {
            _db.SupplyRequestPriorityConfigs.Add(new SupplyRequestPriorityConfig
            {
                UrgentMinutes = 30,
                HighMinutes = 120,
                MediumMinutes = 480,
                UpdatedAt = seed.AnchorUtc
            });
        }

        await SeedServiceZonesAsync(seed.AnchorUtc, cancellationToken);

        if (!await _db.SosPriorityRuleConfigs.AnyAsync(cancellationToken))
        {
            _db.SosPriorityRuleConfigs.Add(new SosPriorityRuleConfig
            {
                ConfigVersion = "SOS_PRIORITY_DEMO_V1",
                IsActive = true,
                CreatedAt = seed.AnchorUtc,
                ActivatedAt = seed.AnchorUtc,
                ConfigJson = Json(new { levels = new[] { "Low", "Medium", "High", "Critical" } }),
                IssueWeightsJson = Json(new { unconscious = 5, drowning = 5, breathingDifficulty = 4, fever = 2, trauma = 4 }),
                MedicalSevereIssuesJson = Json(new[] { "unconscious", "drowning", "breathingDifficulty", "trauma" }),
                AgeWeightsJson = Json(new { child = 1.4, elderly = 1.3, adult = 1.0, pregnant = 1.35 }),
                RequestTypeScoresJson = Json(new { Rescue = 30, Relief = 18, Both = 40 }),
                SituationMultipliersJson = Json(new[]
                {
                    new { keys = new[] { "Flooding", "Stranded" }, multiplier = 1.4, severe = true },
                    new { keys = new[] { "Landslide" }, multiplier = 1.5, severe = true },
                    new { keys = new[] { "CannotMove", "Medical" }, multiplier = 1.3, severe = true }
                }),
                PriorityThresholdsJson = Json(new
                {
                    critical = new { minScore = 80 },
                    high = new { minScore = 60 },
                    medium = new { minScore = 35 },
                    low = new { minScore = 0 }
                }),
                WaterUrgencyScoresJson = Json(new { none = 0, low = 2, medium = 5, high = 8 }),
                FoodUrgencyScoresJson = Json(new { none = 0, oneDay = 3, twoDays = 6, critical = 9 }),
                BlanketUrgencyRulesJson = Json(new { elderly = 4, child = 4, coldRain = 3 }),
                ClothingUrgencyRulesJson = Json(new { soaked = 5, child = 3 }),
                VulnerabilityRulesJson = Json(new { children = 3, elderly = 3, pregnant = 4, injured = 5 }),
                VulnerabilityScoreExpressionJson = "{}",
                ReliefScoreExpressionJson = "{}",
                PriorityScoreExpressionJson = "{}",
                UpdatedAt = seed.AnchorUtc
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedIdentityAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var users = new List<User>();
        users.Add(CreateUser("admin", 1, 1, "Nguyễn", "Minh Tuấn", SeedConstants.AdminPasswordHash, Area(0), seed));

        for (var i = 0; i < 5; i++)
        {
            var name = VietnameseName(i + 3);
            users.Add(CreateUser($"coord{i + 1:00}", 2, i + 1, name.Last, name.First, SeedConstants.CoordinatorPasswordHash, Area(i), seed));
        }

        for (var i = 0; i < 9; i++)
        {
            var name = VietnameseName(i + 20);
            users.Add(CreateUser($"manager{i + 1:00}", 4, i + 1, name.Last, name.First, SeedConstants.ManagerPasswordHash, Area(i + 2), seed));
        }

        for (var i = 0; i < TotalRescuerCount; i++)
        {
            var name = VietnameseName(i + 40);
            var rescuerNumber = i + 1;
            var user = CreateUser($"rescuer{rescuerNumber:000}", 3, rescuerNumber, name.Last, name.First, SeedConstants.RescuerPasswordHash, Area(i), seed);
            if (IsRecentRescuerNumber(rescuerNumber))
            {
                var recentIndex = RecentRescuerIndex(rescuerNumber);
                var createdAt = RecentRescuerCreatedAt(seed, recentIndex);
                user.CreatedAt = createdAt;
                user.UpdatedAt = createdAt.AddHours(8 + recentIndex % 18);
                user.IsEmailVerified = true;
            }

            users.Add(user);
        }

        for (var i = 0; i < 140; i++)
        {
            var name = VietnameseName(i + 150);
            users.Add(CreateUser($"victim{i + 1:000}", 5, i + 1, name.Last, name.First, SeedConstants.VictimPasswordHash, Area(i + 4), seed));
        }

        users[^1].IsBanned = true;
        users[^1].BannedBy = users[0].Id;
        users[^1].BannedAt = seed.AnchorUtc.AddDays(-20);
        users[^1].BanReason = "Tạo nhiều SOS thử nghiệm sai sự thật";
        users[^2].IsBanned = true;
        users[^2].BannedBy = users[0].Id;
        users[^2].BannedAt = seed.AnchorUtc.AddDays(-48);
        users[^2].BanReason = "Spam chat hỗ trợ";

        var demoVictim = CreateDemoVictimWithPin(seed);
        users.Add(demoVictim);

        _db.Users.AddRange(users);
        await _db.SaveChangesAsync(cancellationToken);

        seed.Admins.Add(users[0]);
        seed.Coordinators.AddRange(users.Where(u => u.RoleId == 2));
        seed.Managers.AddRange(users.Where(u => u.RoleId == 4));
        seed.Rescuers.AddRange(users.Where(u => u.RoleId == 3));
        seed.Victims.AddRange(users.Where(u => u.RoleId == 5));

        _db.UserRelativeProfiles.AddRange(CreateDemoVictimRelativeProfiles(demoVictim.Id, seed));
        await _db.SaveChangesAsync(cancellationToken);

        var abilities = await _db.Abilities.OrderBy(a => a.Id).ToListAsync(cancellationToken);
        var userAbilities = new List<UserAbility>();
        foreach (var rescuer in seed.Rescuers)
        {
            var index = seed.Rescuers.IndexOf(rescuer);
            var abilityCount = 2 + index % 5;
            for (var i = 0; i < abilityCount; i++)
            {
                var ability = abilities[(index * 3 + i) % abilities.Count];
                userAbilities.Add(new UserAbility
                {
                    UserId = rescuer.Id,
                    AbilityId = ability.Id,
                    Level = 2 + (index + i) % 4
                });
            }
        }

        _db.UserAbilities.AddRange(userAbilities);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedPersonnelAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var points = new[]
        {
            ("AP-HUE-TD-241015", "Sân vận động Tự Do (Thừa Thiên Huế)", 16.46751083681696, 107.59761456770599, "Available", 20, "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774499522/SVDTD_TTH_sqdeoa.jpg"),
            ("AP-HUE-02", "Trường THCS Hương Sơ", 16.4952, 107.5860, "Available", (int?)null, (string?)null),
            ("AP-HUE-03", "Nhà văn hóa Quảng Điền", 16.5790, 107.5128, "Unavailable", (int?)null, (string?)null),
            ("AP-DNG-01", "Cung thể thao Tiên Sơn", 16.0471, 108.2188, "Available", (int?)null, (string?)null),
            ("AP-DNG-02", "Trung tâm Hòa Vang", 15.9886, 108.1210, "Available", (int?)null, (string?)null),
            ("AP-QTR-01", "Nhà văn hóa Đông Hà", 16.8175, 107.1003, "Available", (int?)null, (string?)null),
            ("AP-QTR-02", "Trường THPT Hải Lăng", 16.6766, 107.2284, "Closed", (int?)null, (string?)null),
            ("AP-QNM-01", "Trung tâm Tam Kỳ", 15.5736, 108.4740, "Available", (int?)null, (string?)null),
            ("AP-QNM-02", "Điểm tập kết Hội An", 15.8801, 108.3380, "Created", (int?)null, (string?)null),
            ("AP-QNG-01", "Trung tâm Quảng Ngãi", 15.1214, 108.8044, "Available", (int?)null, (string?)null)
        };

        foreach (var (code, name, lat, lon, status, maxCapacity, imageUrl) in points)
        {
            seed.AssemblyPoints.Add(new AssemblyPoint
            {
                Code = code,
                Name = name,
                MaxCapacity = maxCapacity ?? 90 + seed.AssemblyPoints.Count * 15,
                Status = status,
                Location = Point(lon, lat),
                CreatedAt = seed.StartUtc.AddDays(seed.AssemblyPoints.Count * 12),
                UpdatedAt = seed.AnchorUtc.AddDays(-seed.AssemblyPoints.Count),
                ImageUrl = imageUrl ?? $"https://cdn.resq.vn/assembly/{code.ToLowerInvariant()}.jpg",
                StatusReason = status == "Unavailable" ? "Đang sửa mái che và máy phát điện" : null,
                StatusChangedAt = seed.AnchorUtc.AddDays(-10 + seed.AssemblyPoints.Count),
                StatusChangedBy = seed.Coordinators[seed.AssemblyPoints.Count % seed.Coordinators.Count].Id
            });
        }

        _db.AssemblyPoints.AddRange(seed.AssemblyPoints);
        await _db.SaveChangesAsync(cancellationToken);

        var deployableRescuers = seed.Rescuers.Take(seed.Rescuers.Count - UnassignedRescuerCount).ToList();
        var standbyRescuers = seed.Rescuers.Skip(deployableRescuers.Count).ToList();
        var standbyRescuerIds = standbyRescuers.Select(r => r.Id).ToHashSet();

        for (var i = 0; i < deployableRescuers.Count; i++)
        {
            deployableRescuers[i].AssemblyPointId = seed.AssemblyPoints[i % seed.AssemblyPoints.Count].Id;
        }

        var profiles = seed.Rescuers.Select((user, index) => new RescuerProfile
        {
            UserId = user.Id,
            RescuerType = index % 4 == 0 ? "Core" : "Volunteer",
            IsEligibleRescuer = index < EligibleAssignedRescuerCount || standbyRescuerIds.Contains(user.Id),
            Step = index < EligibleAssignedRescuerCount || standbyRescuerIds.Contains(user.Id) ? 5 : 4,
            ApprovedBy = seed.Admins[0].Id,
            ApprovedAt = IsRecentRescuerNumber(index + 1)
                ? RecentRescuerApprovedAt(seed, user.CreatedAt, RecentRescuerIndex(index + 1))
                : seed.StartUtc.AddDays(20 + index)
        }).ToList();

        _db.RescuerProfiles.AddRange(profiles);
        await _db.SaveChangesAsync(cancellationToken);

        var applications = new List<RescuerApplication>();
        for (var i = 0; i < 45; i++)
        {
            var approved = i < 35;
            var rejected = i >= 40;
            var userId = approved ? seed.Rescuers[i].Id : seed.Victims[i].Id;
            var submitted = seed.StartUtc.AddDays(50 + i * 9);
            applications.Add(new RescuerApplication
            {
                UserId = userId,
                Status = approved ? "Approved" : rejected ? "Rejected" : "Pending",
                SubmittedAt = submitted,
                ReviewedAt = rejected || approved ? submitted.AddDays(2 + i % 4) : null,
                ReviewedBy = rejected || approved ? seed.Admins[0].Id : null,
                AdminNote = approved ? "Đủ hồ sơ và đã xác minh kỹ năng cơ bản" : rejected ? "Thiếu giấy tờ xác minh" : null
            });
        }

        _db.RescuerApplications.AddRange(applications);
        await _db.SaveChangesAsync(cancellationToken);

        var documents = new List<RescuerApplicationDocument>();
        foreach (var application in applications)
        {
            var typeIds = new[] { 9, 5, 1 + application.Id % 4 };
            foreach (var typeId in typeIds)
            {
                documents.Add(new RescuerApplicationDocument
                {
                    ApplicationId = application.Id,
                    FileTypeId = typeId,
                    FileUrl = $"https://cdn.resq.vn/docs/application-{application.Id}-{typeId}.pdf",
                    UploadedAt = application.SubmittedAt?.AddMinutes(typeId * 7)
                });
            }
        }

        _db.RescuerApplicationDocuments.AddRange(documents);

        var scores = deployableRescuers.Take(72).Select((rescuer, index) =>
        {
            var a = 6.5m + (index % 30) / 10m;
            var b = 6.2m + (index % 25) / 10m;
            var c = 6.0m + (index % 28) / 10m;
            var d = 6.4m + (index % 24) / 10m;
            var e = 6.3m + (index % 26) / 10m;
            return new RescuerScore
            {
                UserId = rescuer.Id,
                ResponseTimeScore = a,
                RescueEffectivenessScore = b,
                DecisionHandlingScore = c,
                SafetyMedicalSkillScore = d,
                TeamworkCommunicationScore = e,
                OverallAverageScore = Math.Round((a + b + c + d + e) / 5m, 2),
                EvaluationCount = index % 26,
                CreatedAt = seed.StartUtc.AddDays(100 + index),
                UpdatedAt = seed.AnchorUtc.AddDays(-index % 40)
            };
        }).ToList();
        _db.RescuerScores.AddRange(scores);

        await SeedAssemblyEventsAsync(seed, cancellationToken);
        await SeedRescueTeamsAsync(seed, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedAssemblyEventsAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var deployableRescuers = GetDeployableRescuers(seed);
        var standbyRescuers = seed.Rescuers.Skip(deployableRescuers.Count).ToList();
        var events = new List<AssemblyEvent>();
        var hueStadium = GetHueStadiumAssemblyPoint(seed);
        AssemblyEvent? activeHueEvent = null;

        if (hueStadium is not null)
        {
            var assemblyDate = TrimUtcToMinute(seed.AnchorUtc.AddMinutes(-30));
            var checkInDeadline = assemblyDate.AddMinutes(45);
            activeHueEvent = new AssemblyEvent
            {
                AssemblyPointId = hueStadium.Id,
                AssemblyDate = assemblyDate,
                Status = "Gathering",
                CreatedBy = seed.Coordinators[0].Id,
                CreatedAt = seed.AnchorUtc.AddHours(-2),
                UpdatedAt = seed.AnchorUtc.AddMinutes(-5),
                CheckInDeadline = checkInDeadline
            };
            events.Add(activeHueEvent);

            foreach (var rescuer in standbyRescuers.Take(HueStadiumCheckedInStandbyRescuerCount))
            {
                rescuer.AssemblyPointId = hueStadium.Id;
            }
        }

        for (var i = 0; i < 44; i++)
        {
            var plannedAssemblyDate = RandomEventUtc(seed, i).AddHours(6 + i % 3);
            var plannedCheckInDeadline = plannedAssemblyDate.AddMinutes(45);
            var status = plannedCheckInDeadline <= seed.AnchorUtc
                ? "Completed"
                : "Gathering";
            var assemblyDate = status == "Gathering"
                ? TrimUtcToMinute(seed.AnchorUtc.AddMinutes(-(26 + i % 15)))
                : plannedAssemblyDate;
            var checkInDeadline = assemblyDate.AddMinutes(45);
            events.Add(new AssemblyEvent
            {
                AssemblyPointId = seed.AssemblyPoints[i % seed.AssemblyPoints.Count].Id,
                AssemblyDate = assemblyDate,
                Status = status,
                CreatedBy = seed.Coordinators[i % seed.Coordinators.Count].Id,
                CreatedAt = assemblyDate.AddHours(-8),
                UpdatedAt = status == "Completed"
                    ? ClampHistoricalUtc(assemblyDate.AddHours(8), assemblyDate, seed.AnchorUtc)
                    : ClampHistoricalUtc(seed.AnchorUtc.AddMinutes(-(10 + i % 5)), assemblyDate, seed.AnchorUtc),
                CheckInDeadline = checkInDeadline
            });
        }

        _db.AssemblyEvents.AddRange(events);
        await _db.SaveChangesAsync(cancellationToken);

        var participants = new List<AssemblyParticipant>();
        if (activeHueEvent is not null)
        {
            foreach (var (rescuer, index) in standbyRescuers.Take(HueStadiumCheckedInStandbyRescuerCount).Select((rescuer, index) => (rescuer, index)))
            {
                participants.Add(new AssemblyParticipant
                {
                    AssemblyEventId = activeHueEvent.Id,
                    RescuerId = rescuer.Id,
                    Status = "CheckedIn",
                    IsCheckedIn = true,
                    CheckInTime = activeHueEvent.AssemblyDate.AddMinutes(5 + index * 2),
                    IsCheckedOut = false,
                    CheckOutTime = null
                });
            }
        }

        foreach (var assemblyEvent in events)
        {
            if (activeHueEvent is not null && assemblyEvent.Id == activeHueEvent.Id)
            {
                continue;
            }

            for (var i = 0; i < 7; i++)
            {
                var rescuer = deployableRescuers[(assemblyEvent.Id * 11 + i) % deployableRescuers.Count];
                var absent = (assemblyEvent.Id + i) % 10 == 0;
                var late = (assemblyEvent.Id + i) % 6 == 0;
                participants.Add(new AssemblyParticipant
                {
                    AssemblyEventId = assemblyEvent.Id,
                    RescuerId = rescuer.Id,
                    Status = absent ? "Absent" : "CheckedIn",
                    IsCheckedIn = !absent,
                    CheckInTime = absent
                        ? null
                        : ClampHistoricalUtc(
                            assemblyEvent.AssemblyDate.AddMinutes(
                                assemblyEvent.Status == "Gathering"
                                    ? late ? 35 : 12 + i * 2
                                    : late ? 55 : 20 + i),
                            assemblyEvent.AssemblyDate,
                            seed.AnchorUtc),
                    IsCheckedOut = !absent && assemblyEvent.Status == "Completed",
                    CheckOutTime = !absent && assemblyEvent.Status == "Completed"
                        ? ClampHistoricalUtc(assemblyEvent.AssemblyDate.AddHours(8), assemblyEvent.AssemblyDate, seed.AnchorUtc)
                        : null
                });
            }
        }

        _db.AssemblyParticipants.AddRange(participants);
    }

    private async Task SeedRescueTeamsAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var deployableRescuers = GetDeployableRescuers(seed);
        var statuses = new[]
        {
            "Available", "Available", "Gathering", "Available", "Gathering",
            "Available", "Gathering", "Available", "Available", "Stuck",
            "Available", "Gathering", "Available", "Gathering", "Available",
            "Available", "Available", "Unavailable", "Disbanded", "Disbanded"
        };
        var types = new[] { "Mixed", "Rescue", "Medical", "Transportation" };

        for (var i = 0; i < 20; i++)
        {
            seed.RescueTeams.Add(new RescueTeam
            {
                AssemblyPointId = seed.AssemblyPoints[i % seed.AssemblyPoints.Count].Id,
                ManagedBy = seed.Coordinators[i % seed.Coordinators.Count].Id,
                Code = $"RT-{Area(i).Code}-{i + 1:00}",
                Name = $"Đội {TeamName(i)} {i + 1}",
                TeamType = types[i % types.Length],
                Status = statuses[i],
                MaxMembers = i >= 17 ? 10 : 8,
                Reason = statuses[i] == "Unavailable" ? "Bảo dưỡng thiết bị và nghỉ luân phiên" : null,
                AssemblyDate = RandomEventUtc(seed, i + 80),
                CreatedAt = seed.StartUtc.AddDays(120 + i),
                UpdatedAt = seed.AnchorUtc.AddDays(-i),
                DisbandAt = statuses[i] == "Disbanded" ? seed.AnchorUtc.AddDays(-50 + i) : null
            });
        }

        _db.RescueTeams.AddRange(seed.RescueTeams);
        await _db.SaveChangesAsync(cancellationToken);

        var memberIndex = 0;
        for (var teamIndex = 0; teamIndex < 20; teamIndex++)
        {
            var team = seed.RescueTeams[teamIndex];
            var count = team.MaxMembers; // Luôn lấp đầy đội theo MaxMembers
            for (var i = 0; i < count; i++)
            {
                var rescuer = teamIndex < 18
                    ? deployableRescuers[memberIndex++ % deployableRescuers.Count]
                    : deployableRescuers[(teamIndex * 13 + i) % deployableRescuers.Count];
                var invitedAt = (team.CreatedAt ?? seed.StartUtc).AddHours(2 + i);
                seed.RescueTeamMembers.Add(new RescueTeamMember
                {
                    TeamId = team.Id,
                    UserId = rescuer.Id,
                    Status = "Accepted",
                    InvitedAt = invitedAt,
                    RespondedAt = invitedAt.AddMinutes(10 + i * 3),
                    IsLeader = i == 0,
                    RoleInTeam = i == 0 ? "Leader" : TeamMemberRole(i, team.TeamType),
                    CheckedIn = team.Status != "Disbanded"
                });
            }
        }

        await AddHueStadiumAvailableReserveTeamsAsync(seed, deployableRescuers, memberIndex, cancellationToken);

        _db.RescueTeamMembers.AddRange(seed.RescueTeamMembers);
    }

    private async Task AddHueStadiumAvailableReserveTeamsAsync(
        DemoSeedContext seed,
        IReadOnlyList<User> deployableRescuers,
        int usedDeployableRescuerCount,
        CancellationToken cancellationToken)
    {
        var hueStadium = GetHueStadiumAssemblyPoint(seed)
            ?? throw new InvalidOperationException("Không tìm thấy điểm tập kết Sân vận động Tự Do trong demo seed.");
        var requiredMemberCount = HueStadiumReserveTeamCount * HueStadiumReserveTeamMemberCount;
        var assignedRescuerIds = seed.RescueTeamMembers.Select(member => member.UserId).ToHashSet();
        var reserveRescuers = deployableRescuers
            .Skip(usedDeployableRescuerCount)
            .Concat(seed.Rescuers
                .Skip(deployableRescuers.Count)
                .Where(rescuer => rescuer.AssemblyPointId == hueStadium.Id))
            .Where(rescuer => !assignedRescuerIds.Contains(rescuer.Id))
            .Take(requiredMemberCount)
            .ToList();

        if (reserveRescuers.Count < requiredMemberCount)
        {
            throw new InvalidOperationException("Không đủ rescuer khả dụng để tạo 2 team Available tại Sân vận động Tự Do.");
        }

        foreach (var rescuer in reserveRescuers)
        {
            rescuer.AssemblyPointId = hueStadium.Id;
        }

        var reserveTeamTypes = new[] { "Mixed", "Rescue" };
        var reserveTeamNames = new[] { "Đội thường trực Tự Do 1", "Đội cơ động Tự Do 2" };
        var reserveTeams = new List<RescueTeam>();
        for (var i = 0; i < HueStadiumReserveTeamCount; i++)
        {
            reserveTeams.Add(new RescueTeam
            {
                AssemblyPointId = hueStadium.Id,
                ManagedBy = seed.Coordinators[i % seed.Coordinators.Count].Id,
                Code = $"{HueStadiumReserveTeamCodePrefix}-{i + 1:00}",
                Name = reserveTeamNames[i],
                TeamType = reserveTeamTypes[i],
                Status = "Available",
                MaxMembers = 6,
                AssemblyDate = seed.AnchorUtc.AddHours(-(i + 1)),
                CreatedAt = seed.AnchorUtc.AddDays(-(i + 1)),
                UpdatedAt = seed.AnchorUtc.AddMinutes(-(10 + i))
            });
        }

        _db.RescueTeams.AddRange(reserveTeams);
        await _db.SaveChangesAsync(cancellationToken);
        seed.RescueTeams.AddRange(reserveTeams);

        for (var teamIndex = 0; teamIndex < reserveTeams.Count; teamIndex++)
        {
            var team = reserveTeams[teamIndex];
            var members = reserveRescuers
                .Skip(teamIndex * HueStadiumReserveTeamMemberCount)
                .Take(HueStadiumReserveTeamMemberCount)
                .ToList();

            for (var memberPosition = 0; memberPosition < members.Count; memberPosition++)
            {
                var rescuer = members[memberPosition];
                var invitedAt = (team.CreatedAt ?? seed.StartUtc).AddHours(2 + memberPosition);
                seed.RescueTeamMembers.Add(new RescueTeamMember
                {
                    TeamId = team.Id,
                    UserId = rescuer.Id,
                    Status = "Accepted",
                    InvitedAt = invitedAt,
                    RespondedAt = invitedAt.AddMinutes(10 + memberPosition * 3),
                    IsLeader = memberPosition == 0,
                    RoleInTeam = memberPosition == 0 ? "Leader" : TeamMemberRole(memberPosition, team.TeamType),
                    CheckedIn = true
                });
            }
        }
    }

    private async Task SeedLogisticsCatalogAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var categoryDefs = new[]
        {
            ("Food",            "Thực phẩm",        "Lương thực, đồ ăn khô, thực phẩm ăn liền"),
            ("Water",           "Nước uống",        "Nước sạch, nước đóng chai, điện giải"),
            ("Medical",         "Y tế",             "Thuốc men, vật tư y tế, bộ sơ cứu"),
            ("Hygiene",         "Vệ sinh cá nhân",  "Khăn giấy, xà phòng, băng vệ sinh, tã"),
            ("Clothing",        "Quần áo",           "Quần áo sạch, áo mưa, đồ giữ ấm cơ bản"),
            ("Shelter",         "Nơi trú ẩn",        "Lều bạt, túi ngủ, vật dụng che chắn"),
            ("RepairTools",     "Công cụ sửa chữa", "Búa, đinh, cưa, dụng cụ khắc phục khẩn cấp"),
            ("RescueEquipment", "Thiết bị cứu hộ",  "Áo phao, xuồng, dây cứu sinh, bộ đàm"),
            ("Heating",         "Sưởi ấm",           "Chăn, bếp dã chiến, vật dụng giữ nhiệt"),
            ("Vehicle",         "Phương tiện",       "Xe tải, xe cứu thương, ca nô, xe địa hình"),
            ("Others",          "Khác",              "Thiết bị hỗ trợ, tín hiệu, chiếu sáng, ghi nhận hiện trường")
        };

        foreach (var (code, name, description) in categoryDefs)
        {
            seed.Categories.Add(new Category
            {
                Code = code,
                Name = name,
                Description = description,
                Quantity = 0,
                CreatedAt = seed.StartUtc,
                UpdatedAt = seed.AnchorUtc,
                CreatedBy = seed.Admins[0].Id,
                UpdatedBy = seed.Admins[0].Id
            });
        }

        _db.Categories.AddRange(seed.Categories);
        await _db.SaveChangesAsync(cancellationToken);

        var targetGroupsByName = (await _db.TargetGroups.OrderBy(t => t.Id).ToListAsync(cancellationToken))
            .ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        var baseItems = BaseItemModels();
        var imageIds = ReliefItemImageIdsInSeedOrder();
        if (imageIds.Count != baseItems.Count)
        {
            throw new InvalidOperationException("Relief item image id mapping must match the seeded item model count.");
        }

        for (var i = 0; i < baseItems.Count; i++)
        {
            var template = baseItems[i];
            var category = seed.Categories.Single(c => c.Code == template.CategoryCode);
            var item = new ItemModel
            {
                CategoryId = category.Id,
                Name = template.Name,
                Description = template.Description,
                Unit = template.Unit,
                ItemType = template.ItemType,
                VolumePerUnit = template.Volume,
                WeightPerUnit = template.Weight,
                ImageUrl = GetReliefItemImageUrl(imageIds[i]) ?? $"https://cdn.resq.vn/items/{Slug(template.Name)}.jpg",
                CreatedAt = seed.StartUtc.AddDays(15 + i),
                UpdatedAt = seed.AnchorUtc.AddDays(-(i % 60)),
                UpdatedBy = seed.Managers[i % seed.Managers.Count].Id
            };

            foreach (var targetGroupName in TargetGroupNamesFor(template))
            {
                if (targetGroupsByName.TryGetValue(targetGroupName, out var targetGroup))
                {
                    item.TargetGroups.Add(targetGroup);
                }
            }

            seed.ItemModels.Add(item);
        }

        _db.ItemModels.AddRange(seed.ItemModels);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedDepotsAndInventoryAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var depotDefs = new[]
        {
            ("Uỷ Ban MTTQVN Tỉnh Thừa Thiên Huế", "46 Đống Đa, TP. Huế, Thừa Thiên Huế", 16.454572773043417, 107.56799781003454, "Available", 1_100_000m, 440_000m, 80_000_000m, 0m, "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498626/uy-ban-nhan-dan-tinh-thua-thien-hue-image-01_wirqah.jpg"),
            ("Ủy ban MTTQVN TP Đà Nẵng", "270 Trưng Nữ Vương, Hải Châu, Đà Nẵng", 16.080298466000496, 108.22283205420794, "Available", 1_000_000m, 480_000m, 60_000_000m, 10_000_000m, "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498625/MTTQVN_nhbg68.jpg"),
            ("Ủy Ban MTTQ Tỉnh Hà Tĩnh", "72 Phan Đình Phùng, TP. Hà Tĩnh, Hà Tĩnh", 18.349622333272194, 105.90102499916586, "Available", 600_000m, 260_000m, 40_000_000m, 0m, "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498522/z7659305045709_172210c769c874e8409fa13adbc8c47c_qieuum.jpg"),
            ("Ủy ban MTTQVN Việt Nam", "46 Tràng Thi, Hoàn Kiếm, Hà Nội", 21.027819, 105.842191, "Available", 1_400_000m, 650_000m, 100_000_000m, 0m, "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498625/MTTQVN_nhbg68.jpg"),
            ("Ủy ban MTTQVN Huyện Thăng Bình", "282 Tiểu La, thị trấn Hà Lam, huyện Thăng Bình, Quảng Nam", 15.6949, 108.4587, "Available", 250_000m, 120_000m, 12_000_000m, 0m, "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498625/MTTQVN_nhbg68.jpg"),
            ("Ủy ban MTTQVN Huyện Quảng Ninh", "TT. Quán Hàu, huyện Quảng Ninh, Quảng Bình", 17.4619, 106.6175, "Available", 280_000m, 140_000m, 14_000_000m, 0m, "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498625/MTTQVN_nhbg68.jpg"),
            ("Ủy ban MTTQVN Tỉnh Nghệ An", "1 Phan Đăng Lưu, TP. Vinh, Nghệ An", 18.6732581, 105.6936046, "Available", 300_000m, 150_000m, 5_000_000m, 0m, "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498625/MTTQVN_nhbg68.jpg"),
            (DepotClosureTestDepotNames[0], "Đại học Phú Yên, TP. Tuy Hòa, Phú Yên", 13.106332, 109.306890, "Available", 520_000m, 210_000m, 18_000_000m, 0m, "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498625/MTTQVN_nhbg68.jpg"),
            (DepotClosureTestDepotNames[1], "Ga đường sắt Sài Gòn, Quận 3, TP. Hồ Chí Minh", 10.782103, 106.678803, "Available", 900_000m, 360_000m, 30_000_000m, 0m, "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774498625/MTTQVN_nhbg68.jpg")
        };
        var fillRatios = new[] { 0.95m, 0.70m, 0.33m, 0.95m, 0.70m, 0.33m, 0.95m, 0.90m, 0.50m };

        for (var i = 0; i < depotDefs.Length; i++)
        {
            var (name, address, lat, lon, status, capacity, weightCapacity, advanceLimit, outstandingAdvanceAmount, imageUrl) = depotDefs[i];
            var fillRatio = fillRatios[i % fillRatios.Length];
            seed.Depots.Add(new Depot
            {
                Name = name,
                Address = address,
                Location = Point(lon, lat),
                Status = status,
                Capacity = capacity,
                CurrentUtilization = decimal.Round(capacity * fillRatio, 2, MidpointRounding.AwayFromZero),
                WeightCapacity = weightCapacity,
                CurrentWeightUtilization = decimal.Round(weightCapacity * fillRatio, 2, MidpointRounding.AwayFromZero),
                AdvanceLimit = advanceLimit,
                OutstandingAdvanceAmount = outstandingAdvanceAmount,
                LastUpdatedAt = seed.AnchorUtc.AddDays(-i),
                CreatedBy = seed.Admins[0].Id,
                LastUpdatedBy = seed.Managers[i % seed.Managers.Count].Id,
                LastStatusChangedBy = seed.Managers[i % seed.Managers.Count].Id,
                ImageUrl = imageUrl
            });
        }

        _db.Depots.AddRange(seed.Depots);
        await _db.SaveChangesAsync(cancellationToken);

        var depotManagers = new List<DepotManager>();
        for (var i = 0; i < seed.Depots.Count; i++)
        {
            depotManagers.Add(new DepotManager
            {
                DepotId = seed.Depots[i].Id,
                UserId = seed.Managers[i].Id,
                AssignedAt = seed.StartUtc.AddDays(30 + i),
                AssignedBy = seed.Admins[0].Id
            });
        }
        depotManagers.Add(new DepotManager { DepotId = seed.Depots[0].Id, UserId = seed.Managers[6].Id, AssignedAt = seed.StartUtc.AddDays(1), UnassignedAt = seed.StartUtc.AddDays(80), AssignedBy = seed.Admins[0].Id, UnassignedBy = seed.Admins[0].Id });
        depotManagers.Add(new DepotManager { DepotId = seed.Depots[3].Id, UserId = seed.Managers[7].Id, AssignedAt = seed.StartUtc.AddDays(10), UnassignedAt = seed.StartUtc.AddDays(95), AssignedBy = seed.Admins[0].Id, UnassignedBy = seed.Admins[0].Id });
        _db.DepotManagers.AddRange(depotManagers);

        var organizations = new List<Organization>();
        for (var i = 0; i < 14; i++)
        {
            organizations.Add(new Organization
            {
                Name = OrganizationName(i),
                Phone = Phone(7, i + 1),
                Email = $"contact{i + 1:00}@cuutro-mientrung.vn",
                IsActive = i % 11 != 0,
                CreatedAt = seed.StartUtc.AddDays(40 + i),
                UpdatedAt = seed.AnchorUtc.AddDays(-i)
            });
        }
        _db.Organizations.AddRange(organizations);
        await _db.SaveChangesAsync(cancellationToken);

        for (var i = 0; i < 90; i++)
        {
            var item = seed.ItemModels[i % seed.ItemModels.Count];
            _db.OrganizationReliefItems.Add(new OrganizationReliefItem
            {
                OrganizationId = organizations[i % organizations.Count].Id,
                ItemModelId = item.Id,
                Quantity = 80 + (i % 12) * 30,
                ReceivedDate = seed.StartUtc.AddDays(100 + i * 5),
                ExpiredDate = item.ItemType == "Consumable" ? seed.AnchorUtc.AddDays(120 + i % 120) : null,
                Notes = "Ủng hộ đợt mưa lũ miền Trung",
                ReceivedBy = seed.Managers[i % seed.Managers.Count].Id,
                CreatedAt = seed.StartUtc.AddDays(100 + i * 5)
            });
        }

        var inventoryTarget = 620;
        for (var depotIndex = 0; depotIndex < seed.Depots.Count; depotIndex++)
        {
            var itemCount = 103 + (depotIndex < 2 ? 1 : 0);
            for (var itemOffset = 0; itemOffset < itemCount && seed.Inventories.Count < inventoryTarget; itemOffset++)
            {
                var item = seed.ItemModels[(depotIndex * 7 + itemOffset) % seed.ItemModels.Count];
                var quantity = item.ItemType == "Reusable" ? 4 + itemOffset % 14 : 160 + (itemOffset % 30) * 20;
                var missionReserved = itemOffset % 9 == 0 ? Math.Min(quantity / 6, 40) : 0;
                var transferReserved = itemOffset % 13 == 0 ? Math.Min(quantity / 10, 25) : 0;
                seed.Inventories.Add(new SupplyInventory
                {
                    DepotId = seed.Depots[depotIndex].Id,
                    ItemModelId = item.Id,
                    Quantity = quantity,
                    MissionReservedQuantity = missionReserved,
                    TransferReservedQuantity = transferReserved,
                    LastStockedAt = seed.AnchorUtc.AddDays(-itemOffset % 90),
                    IsDeleted = false
                });
            }
        }

        var lifeJacketModel = seed.ItemModels.Single(m => m.Name == "Áo phao cứu sinh");
        var blanketModel = seed.ItemModels.Single(m => m.Name == "Chăn ấm giữ nhiệt");
        EnsureEssentialDepotStock(seed, lifeJacketModel, blanketModel);
        EnsureClosureTestDepotsFullInventory(seed);
        ExcludeHueDepotItems(seed);

        _db.SupplyInventories.AddRange(seed.Inventories);
        await _db.SaveChangesAsync(cancellationToken);

        var consumableInventories = seed.Inventories
            .Where(i => seed.ItemModels.First(m => m.Id == i.ItemModelId).ItemType == "Consumable")
            .Take(395)
            .ToList();
        foreach (var inventory in consumableInventories)
        {
            var received = seed.AnchorUtc.AddDays(-30 - seed.Lots.Count % 300);
            var quantity = Math.Max(20, (inventory.Quantity ?? 100) / 2);
            seed.Lots.Add(new SupplyInventoryLot
            {
                SupplyInventoryId = inventory.Id,
                Quantity = quantity,
                RemainingQuantity = Math.Max(0, quantity - inventory.MissionReservedQuantity - inventory.TransferReservedQuantity),
                ReceivedDate = received,
                ExpiredDate = received.AddMonths(6 + seed.Lots.Count % 18),
                SourceType = seed.Lots.Count % 3 == 0 ? "Purchase" : "Donation",
                SourceId = seed.Lots.Count + 1,
                CreatedAt = received
            });
        }
        EnsureEssentialBlanketLots(seed, blanketModel);
        EnsureHueDepotExpiringConsumableLots(seed);
        EnsureClosureTestDepotsConsumableLots(seed);
        _db.SupplyInventoryLots.AddRange(seed.Lots);

        var reusableModels = seed.ItemModels.Where(m => m.ItemType == "Reusable").ToList();
        for (var i = 0; i < 220; i++)
        {
            var item = reusableModels[i % reusableModels.Count];
            var depot = seed.Depots[i % seed.Depots.Count];
            seed.ReusableItems.Add(new ReusableItem
            {
                DepotId = depot.Id,
                ItemModelId = item.Id,
                SerialNumber = $"{Slug(item.Name ?? "item").ToUpperInvariant()}-{Area(i).Code}-{i + 1:00000}",
                Status = i % 17 == 0 ? "Maintenance" : i % 13 == 0 ? "Reserved" : "Available",
                Condition = i % 11 == 0 ? "Fair" : i % 29 == 0 ? "Poor" : "Good",
                Note = i % 17 == 0 ? "Đang kiểm tra sau nhiệm vụ" : null,
                CreatedAt = seed.StartUtc.AddDays(120 + i),
                UpdatedAt = seed.AnchorUtc.AddDays(-i % 90),
                IsDeleted = false
            });
        }
        EnsureLifeJacketReusableUnits(seed, lifeJacketModel);
        EnsureClosureTestDepotsReusableUnits(seed);
        EnsureManagerReturnFixtureReusableUnits(seed);
        ExcludeHueDepotReusableUnits(seed);
        _db.ReusableItems.AddRange(seed.ReusableItems);

        await SeedVatInvoicesAsync(seed);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedVatInvoicesAsync(DemoSeedContext seed)
    {
        var invoices = new List<VatInvoice>();
        for (var i = 0; i < 50; i++)
        {
            var date = DateOnly.FromDateTime(seed.StartUtc.AddDays(180 + i * 17));
            invoices.Add(new VatInvoice
            {
                InvoiceSerial = $"AA/{date.Year % 100:00}E",
                InvoiceNumber = $"{1800 + i:0000000}",
                SupplierName = SupplierName(i),
                SupplierTaxCode = $"330{1234560 + i}",
                InvoiceDate = date,
                TotalAmount = 8_500_000 + i * 420_000,
                FileUrl = $"https://cdn.resq.vn/vat/{date.Year}-{i + 1:000}.pdf",
                CreatedAt = VnToUtc(date.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(9))))
            });
        }

        _db.VatInvoices.AddRange(invoices);
        await _db.SaveChangesAsync();

        foreach (var invoice in invoices)
        {
            for (var j = 0; j < 3; j++)
            {
                var item = seed.ItemModels[(invoice.Id * 5 + j) % seed.ItemModels.Count];
                var quantity = 20 + (invoice.Id + j) % 80;
                var price = item.ItemType == "Reusable" ? 450_000 + j * 250_000 : 12_000 + j * 8_000;
                _db.VatInvoiceItems.Add(new VatInvoiceItem
                {
                    VatInvoiceId = invoice.Id,
                    ItemModelId = item.Id,
                    Quantity = quantity,
                    UnitPrice = price,
                    CreatedAt = invoice.CreatedAt
                });
            }
        }
    }

    private async Task SeedEmergencyAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var (clusterScenarios, sosScenarios) = CreateHueStadiumSosScenarios();
        if (clusterScenarios.Count != HueStadiumSosClusterCount || sosScenarios.Count != HueStadiumSosRequestCount)
        {
            throw new InvalidOperationException(
                $"Hue stadium SOS seed must contain exactly {HueStadiumSosClusterCount} clusters and {HueStadiumSosRequestCount} SOS requests.");
        }

        var createdSos = new List<SosRequest>();

        for (var i = 0; i < clusterScenarios.Count; i++)
        {
            var scenario = clusterScenarios[i];
            var createdAt = VnToUtc(scenario.LocalCreatedAt);
            var cluster = new SosCluster
            {
                CenterLocation = Point(scenario.Longitude, scenario.Latitude),
                RadiusKm = scenario.RadiusKm,
                SeverityLevel = scenario.SeverityLevel,
                WaterLevel = scenario.WaterLevel,
                VictimEstimated = scenario.VictimEstimated,
                ChildrenCount = scenario.ChildrenCount,
                ElderlyCount = scenario.ElderlyCount,
                MedicalUrgencyScore = scenario.MedicalUrgencyScore,
                CreatedAt = createdAt,
                LastUpdatedAt = ClampHistoricalUtc(createdAt.AddHours(1), createdAt, seed.AnchorUtc),
                Status = scenario.Status
            };

            seed.SosClusters.Add(cluster);
        }

        for (var i = 0; i < sosScenarios.Count; i++)
        {
            var scenario = sosScenarios[i];
            var cluster = seed.SosClusters[scenario.ClusterIndex];
            var victim = seed.Victims[scenario.VictimIndex % seed.Victims.Count];
            var reporter = seed.Victims[scenario.ReporterIndex % seed.Victims.Count];
            var coordinator = seed.Coordinators[scenario.CoordinatorIndex % seed.Coordinators.Count];
            var createdAt = VnToUtc(scenario.LocalCreatedAt);
            var receivedAt = ClampHistoricalUtc(createdAt.AddMinutes(1 + i % 4), createdAt, seed.AnchorUtc);
            DateTime? reviewedAt = scenario.Status == SosRequestStatus.Pending.ToString()
                ? null
                : ClampHistoricalUtc(receivedAt.AddMinutes(6 + i % 8), receivedAt, seed.AnchorUtc);
            var lastUpdatedAt = ClampHistoricalUtc(
                reviewedAt?.AddMinutes(scenario.Status == SosRequestStatus.Resolved.ToString() ? 160 + i * 4 : 24 + i * 3)
                    ?? receivedAt.AddMinutes(8 + i),
                reviewedAt ?? receivedAt,
                seed.AnchorUtc);
            var packetId = StableGuid($"packet-hue-tu-do-{i + 1:000}");
            var deviceId = StableGuid($"device-hue-tu-do-{i + 1:000}").ToString().ToUpperInvariant();

            createdSos.Add(new SosRequest
            {
                PacketId = packetId,
                Cluster = cluster,
                UserId = victim.Id,
                Location = Point(scenario.Longitude, scenario.Latitude),
                LocationAccuracy = 6 + i % 9,
                SosType = scenario.SosType,
                RawMessage = BuildHueStadiumRawMessage(scenario),
                StructuredData = BuildHueStadiumStructuredData(scenario),
                NetworkMetadata = BuildHueStadiumNetworkMetadata(scenario, deviceId),
                SenderInfo = BuildHueStadiumSenderInfo(victim, reporter, coordinator, scenario, deviceId),
                VictimInfo = BuildHueStadiumVictimInfo(victim, scenario),
                ReporterInfo = BuildHueStadiumReporterInfo(victim, reporter, coordinator, scenario, deviceId),
                IsSentOnBehalf = scenario.IsSentOnBehalf,
                OriginId = deviceId,
                PriorityLevel = scenario.PriorityLevel,
                PriorityScore = scenario.PriorityScore,
                Status = scenario.Status,
                AiAnalysis = null,
                ReceivedAt = receivedAt,
                Timestamp = new DateTimeOffset(createdAt).ToUnixTimeSeconds(),
                CreatedAt = createdAt,
                LastUpdatedAt = lastUpdatedAt,
                ReviewedAt = reviewedAt,
                ReviewedById = scenario.Status == SosRequestStatus.Pending.ToString() ? null : coordinator.Id,
                CreatedByCoordinatorId = scenario.IsSentOnBehalf ? coordinator.Id : null
            });
        }

        foreach (var cluster in seed.SosClusters)
        {
            var clusterSos = createdSos.Where(sos => ReferenceEquals(sos.Cluster, cluster)).ToList();
            if (clusterSos.Count == 0)
            {
                continue;
            }

            cluster.LastUpdatedAt = clusterSos
                .Select(sos => sos.LastUpdatedAt ?? sos.CreatedAt ?? cluster.CreatedAt ?? seed.AnchorUtc)
                .Max();
        }

        _db.SosClusters.AddRange(seed.SosClusters);
        _db.SosRequests.AddRange(createdSos);
        await _db.SaveChangesAsync(cancellationToken);
        seed.SosRequests.AddRange(createdSos);

        var companions = new List<SosRequestCompanion>();
        for (var i = 0; i < seed.SosRequests.Count; i++)
        {
            var sos = seed.SosRequests[i];
            var companionCount = 1 + i % 3;
            for (var j = 0; j < companionCount; j++)
            {
                var companion = seed.Victims[(i * 5 + j * 11 + 30) % seed.Victims.Count];
                if (companion.Id == sos.UserId)
                {
                    companion = seed.Victims[(i * 5 + j * 11 + 31) % seed.Victims.Count];
                }

                companions.Add(new SosRequestCompanion
                {
                    SosRequestId = sos.Id,
                    UserId = companion.Id,
                    PhoneNumber = companion.Phone,
                    AddedAt = ClampHistoricalUtc(
                        (sos.CreatedAt ?? seed.StartUtc).AddMinutes(4 + j * 3),
                        sos.CreatedAt ?? seed.StartUtc,
                        seed.AnchorUtc)
                });
            }
        }
        _db.SosRequestCompanions.AddRange(companions.GroupBy(c => new { c.SosRequestId, c.UserId }).Select(g => g.First()));

        foreach (var sos in seed.SosRequests)
        {
            var createdAt = sos.CreatedAt ?? seed.StartUtc;
            _db.SosRuleEvaluations.Add(new SosRuleEvaluation
            {
                SosRequestId = sos.Id,
                ConfigVersion = "SOS_PRIORITY_DEMO_V1",
                MedicalScore = sos.PriorityLevel is "Critical" ? 9 : sos.PriorityLevel is "High" ? 7 : 4,
                FoodScore = (sos.Id % 5) + 2,
                InjuryScore = sos.RawMessage?.Contains("bị thương", StringComparison.OrdinalIgnoreCase) == true ? 8 : 1,
                MobilityScore = sos.RawMessage?.Contains("không thể di chuyển", StringComparison.OrdinalIgnoreCase) == true ? 9 : 4,
                EnvironmentScore = sos.PriorityLevel is "Critical" ? 9 : 5,
                TotalScore = sos.PriorityScore,
                PriorityLevel = sos.PriorityLevel,
                RuleVersion = "v1.0",
                ItemsNeeded = BuildHueStadiumRuleItemsNeeded(sos),
                BreakdownJson = Json(new { priority = sos.PriorityLevel, reason = "Curated Hue stadium mobile SOS demo seed" }),
                DetailsJson = sos.StructuredData,
                CreatedAt = ClampHistoricalUtc(createdAt.AddMinutes(1), createdAt, seed.AnchorUtc)
            });

            for (var u = 0; u < 2; u++)
            {
                _db.SosRequestUpdates.Add(new SosRequestUpdate
                {
                    SosRequestId = sos.Id,
                    Type = u == 0 ? "CoordinatorNote" : sos.Status == "Resolved" ? "Rescued" : "TeamApproaching",
                    Content = u == 0 ? "Đã tiếp nhận thông tin và kiểm tra vị trí." : SosUpdateContent(sos.Status),
                    CreatedAt = ClampHistoricalUtc(createdAt.AddMinutes(15 + u * 35), createdAt, seed.AnchorUtc),
                    Status = "Visible"
                });
            }
        }

        foreach (var sos in seed.SosRequests)
        {
            _db.SosAiAnalyses.Add(new SosAiAnalysis
            {
                SosRequestId = sos.Id,
                ModelName = "GeminiPro",
                ModelVersion = "v1.0",
                AnalysisType = "SosAssessment",
                SuggestedSeverityLevel = sos.PriorityLevel,
                SuggestedPriority = sos.PriorityLevel,
                SuggestedPriorityScore = sos.PriorityLevel == "Critical"
                    ? 9.0
                    : sos.PriorityLevel == "High"
                        ? 7.0
                        : sos.PriorityLevel == "Medium"
                            ? 5.0
                            : 2.0,
                AgreesWithRuleBase = true,
                Explanation = $"Đề xuất {sos.PriorityLevel} dựa trên vị trí, khả năng di chuyển và nhóm dễ tổn thương.",
                SuggestionScope = "DemoSeed",
                Metadata = Json(new
                {
                    seed_area = "Sân vận động Tự Do, Huế",
                    risk_factors = new[] { "flood", "vulnerable_people", "limited_access" },
                    mobile_packet = true
                }),
                CreatedAt = ClampHistoricalUtc(
                    (sos.CreatedAt ?? seed.StartUtc).AddMinutes(2),
                    sos.CreatedAt ?? seed.StartUtc,
                    seed.AnchorUtc),
                AdoptedAt = sos.Status == "Pending"
                    ? null
                    : ClampHistoricalUtc(
                        (sos.ReviewedAt ?? sos.CreatedAt)?.AddMinutes(1),
                        sos.ReviewedAt ?? sos.CreatedAt ?? seed.StartUtc,
                        seed.AnchorUtc)
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string BuildHueStadiumStructuredData(HueStadiumSosScenario scenario)
    {
        var peopleCount = CountHueStadiumPeople(scenario.Victims);
        var payload = new Dictionary<string, object?>
        {
            ["incident"] = new Dictionary<string, object?>
            {
                ["address"] = scenario.Address,
                ["people_count"] = new
                {
                    adult = peopleCount.Adult,
                    child = peopleCount.Child,
                    elderly = peopleCount.Elderly
                },
                ["situation"] = scenario.Situation,
                ["can_move"] = scenario.CanMove,
                ["has_injured"] = scenario.HasInjured,
                ["need_medical"] = scenario.NeedMedical,
                ["others_are_stable"] = scenario.OthersAreStable,
                ["additional_description"] = scenario.AdditionalDescription
            },
            ["victims"] = scenario.Victims
                .Select((victim, index) => new Dictionary<string, object?>
                {
                    ["person_id"] = victim.PersonId,
                    ["person_type"] = victim.PersonType,
                    ["index"] = index + 1,
                    ["custom_name"] = victim.CustomName,
                    ["incident_status"] = BuildHueStadiumVictimIncidentStatus(victim),
                    ["personal_needs"] = BuildHueStadiumVictimPersonalNeeds(victim)
                })
                .ToList()
        };

        if (scenario.GroupNeeds.Count > 0)
        {
            payload["group_needs"] = BuildHueStadiumGroupNeeds(scenario);
        }

        return Json(payload);
    }

    private static string BuildHueStadiumNetworkMetadata(
        HueStadiumSosScenario scenario,
        string deviceId)
    {
        return Json(new
        {
            hop_count = scenario.Network == "MESH" ? 1 : 0,
            path = new[] { deviceId }
        });
    }

    private static string BuildHueStadiumSenderInfo(
        User victim,
        User reporter,
        User coordinator,
        HueStadiumSosScenario scenario,
        string deviceId)
    {
        var sender = scenario.IsSentOnBehalf ? coordinator : reporter;
        return Json(new
        {
            device_id = deviceId,
            is_online = scenario.Network != "MESH" && !scenario.IsSentOnBehalf,
            user_id = sender.Id,
            user_name = FullName(sender),
            user_phone = sender.Phone,
            battery_level = scenario.BatteryPercentage
        });
    }

    private static string BuildHueStadiumVictimInfo(User victim, HueStadiumSosScenario scenario)
    {
        return Json(new
        {
            user_id = victim.Id,
            user_name = FullName(victim),
            user_phone = victim.Phone
        });
    }

    private static string BuildHueStadiumReporterInfo(
        User victim,
        User reporter,
        User coordinator,
        HueStadiumSosScenario scenario,
        string deviceId)
    {
        var reporterUser = scenario.IsSentOnBehalf ? coordinator : reporter;
        return Json(new
        {
            device_id = deviceId,
            is_online = scenario.Network != "MESH" && !scenario.IsSentOnBehalf,
            user_id = reporterUser.Id,
            user_name = FullName(reporterUser),
            user_phone = reporterUser.Phone,
            battery_level = scenario.BatteryPercentage
        });
    }

    private static string BuildHueStadiumRawMessage(HueStadiumSosScenario scenario)
    {
        var peopleCount = CountHueStadiumPeople(scenario.Victims);
        var totalPeople = peopleCount.Adult + peopleCount.Child + peopleCount.Elderly;
        var injuredVictims = scenario.Victims
            .Select((victim, index) => (Victim: victim, Index: index + 1, MedicalIssues: HueStadiumMedicalIssuesForVictim(victim)))
            .Where(item => IsHueStadiumVictimInjured(item.Victim, item.MedicalIssues))
            .Select(item => $"{HueStadiumPersonTypeLabel(item.Victim.PersonType)} {item.Index}: {item.Victim.CustomName} - {HueStadiumMedicalIssueLabel(item.MedicalIssues.FirstOrDefault())}")
            .ToList();
        var injuredText = injuredVictims.Count == 0
            ? "Không"
            : string.Join("; ", injuredVictims);

        return $"{HueStadiumSosTypeLabel(scenario.SosType)} | Tình trạng: {HueStadiumSituationLabel(scenario.Situation)} | Số người: {totalPeople} | Bị thương: {injuredText} | Ghi chú: {scenario.AdditionalDescription}";
    }

    private static object BuildHueStadiumGroupNeeds(HueStadiumSosScenario scenario)
    {
        var peopleCount = CountHueStadiumPeople(scenario.Victims);
        var totalPeople = peopleCount.Adult + peopleCount.Child + peopleCount.Elderly;
        var supplies = scenario.GroupNeeds;
        var needsWater = supplies.Contains("DRINKING_WATER", StringComparer.Ordinal);
        var needsFood = supplies.Any(need => need is "READY_TO_EAT_FOOD" or "CHILD_SUPPLIES");
        var needsBlanket = supplies.Contains("BLANKET", StringComparer.Ordinal);
        var needsMedicine = supplies.Contains("MEDICINE", StringComparer.Ordinal)
            || scenario.Victims.Any(victim => HueStadiumMedicalIssuesForVictim(victim).Count > 0);
        var needsClothing = supplies.Contains("DRY_CLOTHES", StringComparer.Ordinal)
            || scenario.Victims.Any(victim => HueStadiumNeedsClothing(victim));

        return new
        {
            supplies,
            water = needsWater
                ? new { duration = "12_TO_24H", remaining = "LOW" }
                : null,
            food = needsFood
                ? new { duration = "12_TO_24H" }
                : null,
            blanket = needsBlanket
                ? new { is_cold_or_wet = true, are_blankets_enough = false, availability = "LOW", request_count = Math.Max(1, Math.Min(totalPeople, 4)) }
                : null,
            medicine = needsMedicine
                ? new
                {
                    needs_urgent_medicine = scenario.NeedMedical,
                    conditions = scenario.Victims.SelectMany(HueStadiumMedicalIssuesForVictim).Distinct().ToList(),
                    medical_needs = scenario.Victims.SelectMany(HueStadiumMedicalIssuesForVictim).Distinct().ToList(),
                    medical_description = scenario.AdditionalDescription
                }
                : null,
            clothing = needsClothing
                ? new { status = "NEEDED", needed_people_count = Math.Max(1, scenario.Victims.Count(HueStadiumNeedsClothing)) }
                : null,
            other_supply_description = scenario.SosType is "Relief" or "Both" ? scenario.AdditionalDescription : null
        };
    }

    private static object BuildHueStadiumVictimIncidentStatus(HueStadiumVictimScenario victim)
    {
        var medicalIssues = HueStadiumMedicalIssuesForVictim(victim);
        var isInjured = IsHueStadiumVictimInjured(victim, medicalIssues);

        return new
        {
            is_injured = isInjured,
            medical_issues = medicalIssues,
            severity = HueStadiumVictimSeverity(victim, isInjured)
        };
    }

    private static object BuildHueStadiumVictimPersonalNeeds(HueStadiumVictimScenario victim)
    {
        var hasSpecialDiet = victim.PersonalNeeds.Any(need => need is "LOW_SALT_FOOD" or "DIABETES_MEDICINE" or "MILK" or "PORRIDGE");

        return new
        {
            clothing = new
            {
                needed = HueStadiumNeedsClothing(victim),
                gender = victim.PersonType == "CHILD" ? "CHILD" : null
            },
            diet = new
            {
                has_special_diet = hasSpecialDiet,
                description = hasSpecialDiet ? HueStadiumDietDescription(victim) : null
            }
        };
    }

    private static (int Adult, int Child, int Elderly) CountHueStadiumPeople(IReadOnlyList<HueStadiumVictimScenario> victims)
    {
        var child = victims.Count(victim => string.Equals(victim.PersonType, "CHILD", StringComparison.Ordinal));
        var elderly = victims.Count(victim => string.Equals(victim.PersonType, "ELDERLY", StringComparison.Ordinal));
        var adult = victims.Count - child - elderly;
        return (Math.Max(0, adult), child, elderly);
    }

    private static bool IsHueStadiumVictimInjured(HueStadiumVictimScenario victim, IReadOnlyCollection<string> medicalIssues)
        => medicalIssues.Count > 0
            || victim.IncidentStatus is "INJURED" or "CRITICAL" or "MODERATE";

    private static bool HueStadiumNeedsClothing(HueStadiumVictimScenario victim)
        => victim.PersonalNeeds.Any(need => need is "DRY_CLOTHES" or "HYPOTHERMIA_BLANKET" or "BLANKET");

    private static string HueStadiumVictimSeverity(HueStadiumVictimScenario victim, bool isInjured)
    {
        if (!isInjured)
        {
            return "LOW";
        }

        return victim.IncidentStatus switch
        {
            "CRITICAL" => "CRITICAL",
            "INJURED" or "MODERATE" => "MODERATE",
            _ => "MILD"
        };
    }

    private static List<string> HueStadiumMedicalIssuesForVictim(HueStadiumVictimScenario victim)
    {
        var needs = victim.PersonalNeeds;
        var issues = new SortedSet<string>(StringComparer.Ordinal);

        if (needs.Any(need => need.Contains("FRACTURE", StringComparison.Ordinal)))
        {
            issues.Add("FRACTURE");
        }

        if (needs.Any(need => need.Contains("HYPOTHERMIA", StringComparison.Ordinal)))
        {
            issues.Add("HYPOTHERMIA");
        }

        if (needs.Any(need => need.Contains("HEART", StringComparison.Ordinal)))
        {
            issues.Add("CARDIOVASCULAR");
        }

        if (needs.Any(need => need.Contains("DIABETES", StringComparison.Ordinal)))
        {
            issues.Add("CHRONIC_DISEASE");
        }

        if (needs.Any(need => need.Contains("FEVER", StringComparison.Ordinal)))
        {
            issues.Add("FEVER");
        }

        if (needs.Any(need => need.Contains("OXYGEN", StringComparison.Ordinal)))
        {
            issues.Add("BREATHING_DIFFICULTY");
        }

        if (needs.Any(need => need.Contains("MATERNITY", StringComparison.Ordinal)) || victim.PersonType == "PREGNANT")
        {
            issues.Add("PREGNANCY");
        }

        if (needs.Any(need => need.Contains("WOUND", StringComparison.Ordinal)
                || need.Contains("FIRST_AID", StringComparison.Ordinal)
                || need.Contains("PAIN", StringComparison.Ordinal))
            || victim.IncidentStatus == "INJURED")
        {
            issues.Add("MINOR_INJURY");
        }

        return issues.ToList();
    }

    private static string HueStadiumSosTypeLabel(string sosType) => sosType switch
    {
        "Relief" => "[CỨU TRỢ]",
        "Both" => "[CỨU HỘ + CỨU TRỢ]",
        _ => "[CỨU HỘ]"
    };

    private static string HueStadiumSituationLabel(string situation) => situation switch
    {
        "TRAPPED" => "Mắc kẹt",
        "STRANDED" => "Bị cô lập",
        "SUPPLY_SHORTAGE" => "Thiếu nhu yếu phẩm",
        "MEDICAL" => "Cần y tế",
        "UNSAFE_ROUTE" => "Đường nguy hiểm",
        "EVACUATION" => "Cần sơ tán",
        _ => situation
    };

    private static string HueStadiumPersonTypeLabel(string personType) => personType switch
    {
        "CHILD" => "Trẻ em",
        "ELDERLY" => "Người già",
        "PREGNANT" => "Phụ nữ mang thai",
        _ => "Người lớn"
    };

    private static string HueStadiumMedicalIssueLabel(string? issue) => issue switch
    {
        "FRACTURE" => "Gãy xương",
        "HYPOTHERMIA" => "Mất nhiệt",
        "CARDIOVASCULAR" => "Tim mạch",
        "CHRONIC_DISEASE" => "Bệnh nền",
        "FEVER" => "Sốt",
        "BREATHING_DIFFICULTY" => "Khó thở",
        "PREGNANCY" => "Thai kỳ",
        "MINOR_INJURY" => "Chấn thương nhẹ",
        _ => "Cần hỗ trợ y tế"
    };

    private static string HueStadiumDietDescription(HueStadiumVictimScenario victim)
    {
        if (victim.PersonalNeeds.Contains("LOW_SALT_FOOD", StringComparer.Ordinal))
        {
            return "Ăn nhạt, hạn chế muối.";
        }

        if (victim.PersonalNeeds.Contains("DIABETES_MEDICINE", StringComparer.Ordinal))
        {
            return "Cần kiểm soát đường huyết và ăn đúng bữa.";
        }

        if (victim.PersonalNeeds.Contains("MILK", StringComparer.Ordinal))
        {
            return "Cần sữa hoặc thức ăn mềm cho trẻ nhỏ.";
        }

        return "Có nhu cầu ăn uống riêng.";
    }

    private static string BuildHueStadiumRuleItemsNeeded(SosRequest sos)
    {
        var items = new SortedSet<string>(StringComparer.Ordinal)
        {
            "LOCATION_VERIFICATION"
        };

        if (!string.IsNullOrWhiteSpace(sos.StructuredData))
        {
            using var document = JsonDocument.Parse(sos.StructuredData);
            if (document.RootElement.TryGetProperty("group_needs", out var groupNeeds)
                && groupNeeds.ValueKind == JsonValueKind.Object
                && groupNeeds.TryGetProperty("supplies", out var supplies)
                && supplies.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in supplies.EnumerateArray())
                {
                    var value = item.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        items.Add(value);
                    }
                }
            }

            if (document.RootElement.TryGetProperty("incident", out var incident))
            {
                if (incident.TryGetProperty("need_medical", out var needMedical) && needMedical.GetBoolean())
                {
                    items.Add("FIRST_AID");
                }

                if (incident.TryGetProperty("can_move", out var canMove) && !canMove.GetBoolean())
                {
                    items.Add("RESCUE_TEAM");
                }
            }
        }

        if (sos.SosType is "Relief" or "Both")
        {
            items.Add("DRINKING_WATER");
            items.Add("READY_TO_EAT_FOOD");
        }

        return Json(items);
    }

    private static string SosAddressFromStructuredData(string? structuredData)
    {
        if (string.IsNullOrWhiteSpace(structuredData))
        {
            return "Khu dân cư quanh Sân vận động Tự Do";
        }

        using var document = JsonDocument.Parse(structuredData);
        if (document.RootElement.TryGetProperty("incident", out var incident)
            && incident.TryGetProperty("address", out var nestedAddress))
        {
            return nestedAddress.GetString() ?? "Khu dân cư quanh Sân vận động Tự Do";
        }

        if (document.RootElement.TryGetProperty("address", out var address))
        {
            return address.GetString() ?? "Khu dân cư quanh Sân vận động Tự Do";
        }

        return "Khu dân cư quanh Sân vận động Tự Do";
    }

    private static (
        IReadOnlyList<HueStadiumClusterScenario> Clusters,
        IReadOnlyList<HueStadiumSosScenario> SosRequests)
        CreateHueStadiumSosScenarios()
    {
        var clusters = new[]
        {
            new HueStadiumClusterScenario("HUE-TD-01", 16.462120, 107.602860, 0.34, "High", "Ngập 0.8m quanh cổng", 9, 2, 1, 0.72, "Pending", new DateTime(2026, 4, 24, 6, 45, 0, DateTimeKind.Unspecified)),
            new HueStadiumClusterScenario("HUE-TD-02", 16.461170, 107.606060, 0.18, "Critical", "Ngập 1.2m phía đông", 4, 0, 0, 0.91, "InProgress", new DateTime(2026, 4, 24, 7, 5, 0, DateTimeKind.Unspecified)),
            new HueStadiumClusterScenario("HUE-TD-03", 16.458620, 107.601700, 0.24, "High", "Nước rút còn 0.4m", 7, 1, 1, 0.61, "Completed", new DateTime(2026, 4, 24, 7, 40, 0, DateTimeKind.Unspecified)),
            new HueStadiumClusterScenario("HUE-TD-04", 16.462630, 107.603940, 0.42, "High", "Ngập kiệt nhỏ, có điểm dây điện võng thấp", 9, 0, 1, 0.70, "Pending", new DateTime(2026, 4, 24, 8, 15, 0, DateTimeKind.Unspecified)),
            new HueStadiumClusterScenario("HUE-TD-05", 16.459900, 107.604470, 0.24, "High", "Nước chảy xiết", 8, 1, 1, 0.79, "InProgress", new DateTime(2026, 4, 24, 9, 0, 0, DateTimeKind.Unspecified)),
            new HueStadiumClusterScenario("HUE-TD-06", 16.465500, 107.603400, 0.22, "Medium", "Ngập cục bộ", 5, 1, 1, 0.42, "Completed", new DateTime(2026, 4, 24, 9, 35, 0, DateTimeKind.Unspecified)),
            new HueStadiumClusterScenario("HUE-TD-07", 16.457760, 107.606050, 0.16, "Critical", "Ngập sâu 1.4m", 5, 0, 0, 0.88, "InProgress", new DateTime(2026, 4, 24, 10, 20, 0, DateTimeKind.Unspecified)),
            new HueStadiumClusterScenario("HUE-TD-08", 16.466740, 107.598890, 0.20, "Low", "Ngập rải rác", 2, 0, 0, 0.26, "InProgress", new DateTime(2026, 4, 24, 11, 5, 0, DateTimeKind.Unspecified)),
            new HueStadiumClusterScenario("HUE-TD-09", 16.462260, 107.602950, 0.38, "Medium", "Ngập quanh các kiệt nhỏ", 12, 2, 1, 0.55, "InProgress", new DateTime(2026, 4, 24, 11, 30, 0, DateTimeKind.Unspecified)),
            new HueStadiumClusterScenario("HUE-TD-10", 16.460740, 107.606690, 0.16, "Critical", "Ngập sâu phía đông", 6, 1, 1, 0.90, "InProgress", new DateTime(2026, 4, 24, 11, 55, 0, DateTimeKind.Unspecified)),
            new HueStadiumClusterScenario("HUE-TD-11", 16.458300, 107.606000, 0.22, "High", "Ngập sâu ở cổng phụ phía nam", 8, 1, 1, 0.76, "InProgress", new DateTime(2026, 4, 24, 12, 20, 0, DateTimeKind.Unspecified))
        };

        var sosRequests = new[]
        {
            new HueStadiumSosScenario(0, 16.462310, 107.602510, "Cổng chính Sân vận động Tự Do, đường Lê Quý Đôn, Huế", "Rescue", "Pending", "High", 74, "TRAPPED", false, true, true, false, "Ba người kẹt ở tầng trệt, nước dâng nhanh và có một người bị rách chân.", "Gia đình tôi ở sát cổng chính sân Tự Do, nước vào nhà gần tới thắt lưng. Có 3 người, một người bị rách chân, cần xuồng tiếp cận gấp.", "4G", 34, false, 2, 2, 0, new DateTime(2026, 4, 24, 7, 12, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("self", "ADULT", "Anh Minh", "TRAPPED", ["FIRST_AID", "EVACUATION_SUPPORT"]), new HueStadiumVictimScenario("mother", "ELDERLY", "Bà Lan", "INJURED", ["WHEELCHAIR_SUPPORT", "BLOOD_PRESSURE_MEDICINE"]), new HueStadiumVictimScenario("child", "CHILD", "Bé Nam", "SCARED", ["CHILD_LIFE_JACKET"])], []),
            new HueStadiumSosScenario(0, 16.461880, 107.603040, "Kiệt 18 Lê Quý Đôn, cạnh khán đài A Sân Tự Do, Huế", "Both", "Pending", "High", 70, "STRANDED", false, false, false, true, "Nhóm bốn người mắc kẹt trên gác, còn nước uống khoảng nửa ngày.", "Nhà trong kiệt cạnh khán đài A bị ngập, bốn người đang ở trên gác. Cần đội cứu hộ kiểm tra và mang nước uống, đồ ăn khô.", "WIFI", 58, false, 3, 4, 1, new DateTime(2026, 4, 24, 7, 28, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("self", "ADULT", "Chị Hạnh", "TRAPPED", ["DRINKING_WATER"]), new HueStadiumVictimScenario("father", "ELDERLY", "Ông Phú", "STABLE", ["LOW_SALT_FOOD"]), new HueStadiumVictimScenario("child-1", "CHILD", "Bé My", "STABLE", ["MILK"]), new HueStadiumVictimScenario("child-2", "CHILD", "Bé Bo", "STABLE", ["CHILD_MEDICINE"])], ["DRINKING_WATER", "READY_TO_EAT_FOOD", "CHILD_SUPPLIES"]),
            new HueStadiumSosScenario(8, 16.462530, 107.603260, "Nhà số 7 hẻm sau cổng Sân Tự Do, phường Phú Nhuận, Huế", "Relief", "Pending", "Medium", 57, "SUPPLY_SHORTAGE", true, false, false, true, "Điểm trú tạm thiếu nước sạch, pin sạc và chăn cho trẻ nhỏ.", "Chúng tôi đã lên tầng hai an toàn nhưng có 6 người trú tạm, thiếu nước sạch, chăn và pin sạc điện thoại từ sáng.", "MESH", 22, false, 4, 4, 2, new DateTime(2026, 4, 24, 7, 51, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("group-lead", "ADULT", "Cô Thảo", "SAFE", ["POWER_BANK"]), new HueStadiumVictimScenario("older-neighbor", "ELDERLY", "Bác Năm", "STABLE", ["BLANKET"])], ["DRINKING_WATER", "BLANKET", "POWER_BANK"]),
            new HueStadiumSosScenario(1, 16.461170, 107.606060, "Đường Hà Huy Tập đoạn sát Sân Tự Do, Huế", "Rescue", "Assigned", "Critical", 91, "TRAPPED", false, true, true, false, "Một người lớn bị gãy tay nghi ngờ, nhóm đang bám lan can trước nhà.", "Nhà tôi ở đoạn Hà Huy Tập sát sân, nước chảy mạnh. Có người nghi gãy tay, không thể tự ra ngoài, xin đội cứu hộ đến ngay.", "5G", 46, false, 5, 5, 3, new DateTime(2026, 4, 24, 8, 6, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("self", "ADULT", "Anh Dũng", "INJURED", ["FRACTURE_SPLINT", "PAIN_RELIEF"]), new HueStadiumVictimScenario("wife", "ADULT", "Chị Mai", "TRAPPED", ["EVACUATION_SUPPORT"])], []),
            new HueStadiumSosScenario(9, 16.460740, 107.606690, "Hẻm 24 Hà Huy Tập, phía đông Sân Tự Do, Huế", "Both", "InProgress", "Critical", 88, "MEDICAL", false, true, true, false, "Cụ ông khó thở sau khi ngâm nước lâu, cần sơ cứu và áo phao để đưa ra.", "Cụ ông 78 tuổi khó thở, gia đình 5 người bị kẹt trong hẻm 24 Hà Huy Tập. Cần y tế và áo phao trẻ em.", "4G", 41, true, 6, 7, 4, new DateTime(2026, 4, 24, 8, 34, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("grandfather", "ELDERLY", "Ông Tịnh", "CRITICAL", ["OXYGEN_CHECK", "HEART_MEDICINE"]), new HueStadiumVictimScenario("adult-1", "ADULT", "Chị Ngọc", "TRAPPED", ["EVACUATION_SUPPORT"]), new HueStadiumVictimScenario("child-1", "CHILD", "Bé Su", "STABLE", ["CHILD_LIFE_JACKET"])], ["LIFE_JACKET", "DRINKING_WATER", "MEDICINE"]),
            new HueStadiumSosScenario(3, 16.461420, 107.606870, "Tổ dân phố sau Trường THCS gần Sân Tự Do, Huế", "Rescue", "Incident", "High", 76, "UNSAFE_ROUTE", true, false, false, true, "Đường vào bị dây điện võng thấp, cần đội kiểm tra trước khi sơ tán.", "Lối vào xóm phía sau trường gần sân Tự Do có dây điện võng xuống nước. Gia đình còn trong nhà, chưa dám di chuyển.", "4G", 63, false, 8, 8, 0, new DateTime(2026, 4, 24, 8, 58, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("self", "ADULT", "Anh Khánh", "AT_RISK", ["ROUTE_CLEARANCE"]), new HueStadiumVictimScenario("mother", "ELDERLY", "Mẹ Khánh", "STABLE", ["ESCORT_SUPPORT"])], []),
            new HueStadiumSosScenario(2, 16.458740, 107.601460, "Kiệt 5 Nguyễn Huệ nối về Sân Tự Do, Huế", "Rescue", "Resolved", "High", 69, "EVACUATION", true, true, true, true, "Hai người đã được đưa ra khỏi vùng ngập, còn cần ghi nhận y tế sau sơ cứu.", "Hai người trong kiệt 5 Nguyễn Huệ đã được đội xuồng đưa ra, một người trầy chân đã băng bó tạm.", "4G", 77, false, 9, 9, 1, new DateTime(2026, 4, 24, 9, 18, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("self", "ADULT", "Chị Duyên", "RESCUED", ["WOUND_CLEANING"]), new HueStadiumVictimScenario("child", "CHILD", "Bé Linh", "RESCUED", ["DRY_CLOTHES"])], []),
            new HueStadiumSosScenario(2, 16.458420, 107.601940, "Nhà trọ sau Sân Tự Do, gần Nguyễn Huệ, Huế", "Both", "Resolved", "Medium", 52, "SUPPLY_SHORTAGE", true, false, false, true, "Nhóm sinh viên đã nhận nước và được hướng dẫn ra điểm tập kết.", "Nhóm sinh viên ở nhà trọ sau sân Tự Do thiếu nước và mì, đã được đội hỗ trợ chuyển đến điểm tập kết.", "WIFI", 69, false, 10, 10, 2, new DateTime(2026, 4, 24, 9, 31, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("group-lead", "ADULT", "Bạn Hoàng", "RESCUED", ["DRINKING_WATER"]), new HueStadiumVictimScenario("roommate", "ADULT", "Bạn Phúc", "RESCUED", ["READY_TO_EAT_FOOD"])], ["DRINKING_WATER", "READY_TO_EAT_FOOD"]),
            new HueStadiumSosScenario(8, 16.464260, 107.600470, "Góc Nguyễn Huệ - Lê Quý Đôn, cách Sân Tự Do 300m, Huế", "Relief", "Pending", "Medium", 49, "SUPPLY_SHORTAGE", true, false, false, true, "Một điểm trú tạm 7 người cần nước, cháo ăn liền và thuốc hạ sốt.", "Điểm trú ở góc Nguyễn Huệ - Lê Quý Đôn có 7 người, trong đó có trẻ nhỏ. Cần nước sạch, cháo ăn liền, thuốc hạ sốt.", "5G", 52, false, 11, 12, 3, new DateTime(2026, 4, 24, 10, 3, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("adult-1", "ADULT", "Cô Lệ", "SAFE", ["FEVER_MEDICINE"]), new HueStadiumVictimScenario("child-1", "CHILD", "Bé Bảo", "STABLE", ["PORRIDGE", "MILK"])], ["DRINKING_WATER", "READY_TO_EAT_FOOD", "MEDICINE"]),
            new HueStadiumSosScenario(3, 16.463840, 107.601010, "Sau dãy quán đường Nguyễn Huệ gần Sân Tự Do, Huế", "Rescue", "Pending", "High", 72, "TRAPPED", false, false, false, true, "Hai người bị kẹt trong quán, cửa cuốn hỏng do mất điện.", "Hai người đang kẹt trong quán phía sau Nguyễn Huệ, cửa cuốn không mở vì mất điện, nước ngoài đường lên nhanh.", "MESH", 28, false, 12, 12, 4, new DateTime(2026, 4, 24, 10, 22, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("owner", "ADULT", "Anh Sơn", "TRAPPED", ["DOOR_OPENING_SUPPORT"]), new HueStadiumVictimScenario("staff", "ADULT", "Bạn Vy", "TRAPPED", ["EVACUATION_SUPPORT"])], []),
            new HueStadiumSosScenario(4, 16.459620, 107.604620, "Hẻm nhỏ sau đường Hà Huy Tập, phường Phú Nhuận, Huế", "Both", "Assigned", "High", 73, "STRANDED", false, true, true, false, "Nhà có sản phụ đau bụng nhẹ, nhóm cần được đưa ra và nhận nước sạch.", "Sản phụ trong nhà đau bụng nhẹ, nước ngập qua đầu gối và có trẻ nhỏ. Cần đội đưa ra điểm an toàn, mang thêm nước sạch.", "4G", 49, true, 13, 14, 0, new DateTime(2026, 4, 24, 10, 49, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("pregnant", "PREGNANT", "Chị Hà", "MODERATE", ["MATERNITY_SUPPORT", "FIRST_AID"]), new HueStadiumVictimScenario("child", "CHILD", "Bé Sóc", "STABLE", ["CHILD_LIFE_JACKET"])], ["DRINKING_WATER", "LIFE_JACKET"]),
            new HueStadiumSosScenario(8, 16.459980, 107.605120, "Tầng trệt nhà số 12 Hà Huy Tập, Huế", "Relief", "InProgress", "Medium", 58, "SUPPLY_SHORTAGE", true, false, false, true, "Năm người ở tầng hai thiếu thuốc tiểu đường và nước sạch.", "Gia đình 5 người đã lên tầng hai, đang thiếu nước uống và thuốc tiểu đường cho người lớn tuổi.", "WIFI", 71, false, 14, 14, 1, new DateTime(2026, 4, 24, 11, 18, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("father", "ELDERLY", "Ông Bảy", "STABLE", ["DIABETES_MEDICINE"]), new HueStadiumVictimScenario("adult", "ADULT", "Chị Loan", "SAFE", ["DRINKING_WATER"])], ["DRINKING_WATER", "MEDICINE", "READY_TO_EAT_FOOD"]),
            new HueStadiumSosScenario(4, 16.460180, 107.604310, "Kiệt 31 Hà Huy Tập nhìn sang Sân Tự Do, Huế", "Rescue", "Pending", "High", 68, "UNSAFE_ROUTE", false, false, false, true, "Cầu thang ngoài bị ngập, người già không thể xuống tầng trệt.", "Người già trong nhà không thể xuống cầu thang ngoài vì nước xiết. Cần đội có dây hỗ trợ tiếp cận.", "4G", 37, false, 15, 16, 2, new DateTime(2026, 4, 24, 11, 46, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("grandmother", "ELDERLY", "Bà Cúc", "TRAPPED", ["ROPE_ASSIST", "ESCORT_SUPPORT"]), new HueStadiumVictimScenario("adult", "ADULT", "Anh Tú", "AT_RISK", ["EVACUATION_SUPPORT"])], []),
            new HueStadiumSosScenario(5, 16.465260, 107.603120, "Đường Trần Cao Vân gần lối vào Sân Tự Do, Huế", "Relief", "Resolved", "Medium", 44, "SUPPLY_SHORTAGE", true, false, false, true, "Điểm trú đã nhận nước và mì, không còn yêu cầu mở.", "Điểm trú trên Trần Cao Vân đã nhận nước và mì từ đội hỗ trợ, mọi người an toàn.", "5G", 80, false, 16, 16, 3, new DateTime(2026, 4, 24, 12, 9, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("adult", "ADULT", "Chú Lộc", "RESCUED", ["DRINKING_WATER"]), new HueStadiumVictimScenario("elderly", "ELDERLY", "Bà Tâm", "RESCUED", ["BLANKET"])], ["DRINKING_WATER", "READY_TO_EAT_FOOD"]),
            new HueStadiumSosScenario(5, 16.465720, 107.603640, "Sau khu nhà thi đấu phụ Sân Tự Do, Huế", "Rescue", "Resolved", "Medium", 47, "EVACUATION", true, false, false, true, "Hai người đã tự ra theo hướng dẫn của đội, không cần thêm cứu hộ.", "Hai người ở sau nhà thi đấu phụ đã được hướng dẫn ra đường cao hơn, hiện an toàn.", "4G", 66, false, 17, 17, 4, new DateTime(2026, 4, 24, 12, 27, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("self", "ADULT", "Anh Quốc", "RESCUED", ["CHECK_IN"]), new HueStadiumVictimScenario("wife", "ADULT", "Chị Nhi", "RESCUED", ["CHECK_IN"])], []),
            new HueStadiumSosScenario(6, 16.457760, 107.606050, "Đoạn Lê Quý Đôn gần cổng phụ Sân Tự Do, Huế", "Both", "Incident", "Critical", 92, "MEDICAL", false, true, true, false, "Một người bị hạ thân nhiệt, đội báo cần cáng mềm và áo giữ nhiệt.", "Có người ngâm nước lâu bị lạnh run, lơ mơ. Đội đang tiếp cận nhưng cần thêm cáng mềm, áo giữ nhiệt và nước ấm.", "4G", 44, false, 18, 18, 0, new DateTime(2026, 4, 24, 13, 5, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("patient", "ADULT", "Anh Tài", "CRITICAL", ["HYPOTHERMIA_BLANKET", "STRETCHER"]), new HueStadiumVictimScenario("sister", "ADULT", "Chị Trâm", "TRAPPED", ["EVACUATION_SUPPORT"])], ["BLANKET", "MEDICINE", "DRINKING_WATER"]),
            new HueStadiumSosScenario(10, 16.458080, 107.606420, "Hẻm thấp phía nam Sân Tự Do, Huế", "Rescue", "InProgress", "High", 78, "TRAPPED", false, false, false, true, "Ba người bị cô lập, điểm đón phù hợp là cổng phụ phía nam.", "Ba người bị cô lập trong hẻm thấp phía nam sân. Nước xoáy ở đầu hẻm, cần xuồng nhỏ vào cổng phụ.", "MESH", 19, false, 19, 20, 1, new DateTime(2026, 4, 24, 13, 33, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("adult-1", "ADULT", "Anh Lâm", "TRAPPED", ["BOAT_RESCUE"]), new HueStadiumVictimScenario("adult-2", "ADULT", "Chị Yến", "TRAPPED", ["BOAT_RESCUE"]), new HueStadiumVictimScenario("elderly", "ELDERLY", "Ông Hòa", "TRAPPED", ["ESCORT_SUPPORT"])], []),
            new HueStadiumSosScenario(7, 16.466530, 107.598650, "Đường Đống Đa hướng về Sân Tự Do, Huế", "Relief", "Resolved", "Low", 33, "SUPPLY_SHORTAGE", true, false, false, true, "Yêu cầu nước sạch đã được nhóm địa phương xử lý.", "Khu Đống Đa đã nhận nước sạch từ nhóm địa phương, cập nhật để đóng yêu cầu.", "5G", 83, false, 20, 20, 2, new DateTime(2026, 4, 24, 14, 2, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("adult", "ADULT", "Cô Vân", "RESCUED", ["DRINKING_WATER"])], ["DRINKING_WATER"]),
            new HueStadiumSosScenario(7, 16.466940, 107.599120, "Kiệt 44 Đống Đa, cách Sân Tự Do 600m, Huế", "Rescue", "Cancelled", "Low", 26, "EVACUATION", true, false, false, true, "Người gửi báo đã tự di chuyển ra khỏi khu ngập, không cần đội đến.", "Tôi đã tự ra khỏi kiệt 44 Đống Đa nhờ hàng xóm hỗ trợ, xin hủy yêu cầu cứu hộ.", "WIFI", 61, false, 21, 21, 3, new DateTime(2026, 4, 24, 14, 26, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("self", "ADULT", "Anh Huy", "SAFE", ["CHECK_IN"])], []),
            new HueStadiumSosScenario(10, 16.466520, 107.599430, "Nhà dân sau khu Đống Đa, gần Sân Tự Do, Huế", "Both", "InProgress", "Medium", 63, "STRANDED", false, false, false, true, "Bốn người chờ đội ở tầng hai, cần nước uống và đèn pin vì mất điện.", "Bốn người đang chờ ở tầng hai khu Đống Đa gần sân Tự Do. Nhà mất điện, cần nước uống và đèn pin khi đội tới.", "4G", 39, true, 22, 23, 4, new DateTime(2026, 4, 24, 14, 51, 0, DateTimeKind.Unspecified), [new HueStadiumVictimScenario("adult-1", "ADULT", "Chị Nương", "TRAPPED", ["FLASHLIGHT"]), new HueStadiumVictimScenario("adult-2", "ADULT", "Anh Bình", "TRAPPED", ["DRINKING_WATER"]), new HueStadiumVictimScenario("child", "CHILD", "Bé Kem", "STABLE", ["CHILD_LIFE_JACKET"])], ["DRINKING_WATER", "FLASHLIGHT", "LIFE_JACKET"])
        };

        return (clusters, sosRequests);
    }

    private async Task SeedMissionsAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var missionClusters = seed.SosClusters.ToList();
        for (var i = 0; i < missionClusters.Count; i++)
        {
            var cluster = missionClusters[i];
            var createdAt = (cluster.CreatedAt ?? seed.StartUtc).AddMinutes(18);
            var status = i switch
            {
                0 or 3 => "Planned",
                1 or 4 or 6 => "OnGoing",
                2 or 5 => "Completed",
                _ => "Incompleted"
            };
            seed.Missions.Add(new Mission
            {
                ClusterId = cluster.Id,
                MissionType = MissionType(i, cluster.SeverityLevel),
                PriorityScore = PriorityScore(cluster.SeverityLevel ?? "Medium", i),
                Status = status,
                StartTime = status == "Planned" ? null : createdAt.AddMinutes(15),
                ExpectedEndTime = createdAt.AddHours(5 + i % 5),
                IsCompleted = status == "Completed",
                CreatedById = seed.Coordinators[i % seed.Coordinators.Count].Id,
                CreatedAt = createdAt,
                CompletedAt = status == "Completed" ? createdAt.AddHours(5 + i % 5) : null
            });
        }

        _db.Missions.AddRange(seed.Missions);
        await _db.SaveChangesAsync(cancellationToken);

        var supplyItems = seed.ItemModels.Where(m => m.ItemType == "Consumable").Take(45).ToList();
        foreach (var mission in seed.Missions)
        {
            for (var j = 0; j < 2; j++)
            {
                var item = supplyItems[(mission.Id + j * 7) % supplyItems.Count];
                var inventory = seed.Inventories.First(i => i.ItemModelId == item.Id);
                _db.MissionItems.Add(new MissionItem
                {
                    MissionId = mission.Id,
                    ItemModelId = item.Id,
                    RequiredQuantity = 60 + (mission.Id + j) % 180,
                    AllocatedQuantity = 50 + (mission.Id + j) % 150,
                    SourceDepotId = inventory.DepotId,
                    BufferRatio = 0.10 + (j * 0.05)
                });
            }
        }

        for (var i = 0; i < seed.Missions.Count; i++)
        {
            var teamsForMission = i < 40 ? 2 : 1;
            for (var j = 0; j < teamsForMission; j++)
            {
                var team = TeamForMission(seed, i, j);
                var cluster = seed.SosClusters.First(c => c.Id == seed.Missions[i].ClusterId);
                var status = seed.Missions[i].Status switch
                {
                    "Completed" => i % 3 == 0 ? "Reported" : "CompletedWaitingReport",
                    "OnGoing" => "InProgress",
                    "Incompleted" => "Cancelled",
                    _ => "Assigned"
                };
                seed.MissionTeams.Add(new MissionTeam
                {
                    MissionId = seed.Missions[i].Id,
                    RescuerTeamId = team.Id,
                    TeamType = team.TeamType,
                    CurrentLocation = OffsetPoint(cluster.CenterLocation, 0.004 * (j + 1), -0.003 * (j + 1)),
                    LocationUpdatedAt = (seed.Missions[i].StartTime ?? seed.Missions[i].CreatedAt)?.AddMinutes(50),
                    LocationSource = "GPS",
                    Status = status,
                    AssignedAt = seed.Missions[i].CreatedAt?.AddMinutes(10 + j * 8),
                    UnassignedAt = status == "Cancelled" ? seed.Missions[i].CreatedAt?.AddHours(2) : null,
                    Note = "Giao đội theo năng lực và khoảng cách demo",
                    CreatedAt = seed.Missions[i].CreatedAt
                });
            }
        }

        _db.MissionTeams.AddRange(seed.MissionTeams);
        await _db.SaveChangesAsync(cancellationToken);

        SyncRescueTeamStatusesFromAssignments(seed);

        foreach (var missionTeam in seed.MissionTeams)
        {
            var sourceMembers = seed.RescueTeamMembers.Where(m => m.TeamId == missionTeam.RescuerTeamId).Take(5).ToList();
            foreach (var member in sourceMembers)
            {
                _db.MissionTeamMembers.Add(new MissionTeamMember
                {
                    MissionTeamId = missionTeam.Id,
                    RescuerId = member.UserId,
                    RoleInTeam = member.RoleInTeam,
                    JoinedAt = missionTeam.AssignedAt?.AddMinutes(5),
                    LeftAt = missionTeam.Status is "Reported" or "CompletedWaitingReport" ? missionTeam.AssignedAt?.AddHours(7) : null
                });
            }
        }

        foreach (var mission in seed.Missions)
        {
            var missionTeams = seed.MissionTeams.Where(t => t.MissionId == mission.Id).ToList();
            var missionIndex = seed.Missions.IndexOf(mission);
            var activities = mission.Status == "OnGoing" || missionIndex < 2 ? 5 : 4;
            var clusterSos = seed.SosRequests.Where(s => s.ClusterId == mission.ClusterId).ToList();
            for (var step = 1; step <= activities; step++)
            {
                var team = missionTeams[(step - 1) % missionTeams.Count];
                var sos = clusterSos[(step - 1) % clusterSos.Count];
                var type = ActivityType(step, activities, mission.MissionType);
                var hasDepot = type is "COLLECT_SUPPLIES" or "DELIVER_SUPPLIES" or "RETURN_SUPPLIES";
                var depot = hasDepot
                    ? OperationalDepotForActivity(seed, mission.Id, step)
                    : seed.Depots[(mission.Id + step) % seed.Depots.Count];
                var activityStatus = ActivityStatusFor(mission.Status, step, activities);
                var assigned = (mission.StartTime ?? mission.CreatedAt)?.AddMinutes(step * 35);
                seed.MissionActivities.Add(new MissionActivity
                {
                    MissionId = mission.Id,
                    Step = step,
                    ActivityType = type,
                    Description = ActivityDescription(type, depot.Name, sos.RawMessage),
                    Target = Json(new { address = SosAddressFromStructuredData(sos.StructuredData), sos_request_id = sos.Id }),
                    Items = hasDepot ? Json(new[] { new SupplyToCollectDto { ItemId = seed.ItemModels[(mission.Id + step) % seed.ItemModels.Count].Id, ItemName = seed.ItemModels[(mission.Id + step) % seed.ItemModels.Count].Name ?? "Vật phẩm", Quantity = 20 + step * 10, Unit = "đơn vị" } }) : null,
                    TargetLocation = hasDepot ? depot.Location : sos.Location,
                    Status = activityStatus,
                    AssignedAt = assigned,
                    CompletedAt = activityStatus is "Succeed" ? assigned?.AddMinutes(40 + step * 10) : null,
                    LastDecisionBy = seed.Coordinators[mission.Id % seed.Coordinators.Count].Id,
                    MissionTeamId = team.Id,
                    Priority = mission.PriorityScore >= 80 ? "Critical" : mission.PriorityScore >= 60 ? "High" : "Medium",
                    EstimatedTime = 35 + step * 15,
                    SosRequestId = sos.Id,
                    DepotId = hasDepot ? depot.Id : null,
                    DepotName = hasDepot ? depot.Name : null,
                    DepotAddress = hasDepot ? depot.Address : null,
                    AssemblyPointId = seed.RescueTeams.First(t => t.Id == team.RescuerTeamId).AssemblyPointId
                });
            }
        }

        _db.MissionActivities.AddRange(seed.MissionActivities);
        await _db.SaveChangesAsync(cancellationToken);

        await SeedTestActivityStatusesAsync(seed, cancellationToken);

        for (var i = 0; i < 35; i++)
        {
            var team = seed.MissionTeams.Where(t => t.Status is "Assigned" or "InProgress").ElementAt(i % seed.MissionTeams.Count(t => t.Status is "Assigned" or "InProgress"));
            var activity = seed.MissionActivities.First(a => a.MissionTeamId == team.Id);
            var support = i % 4 == 0 ? seed.SosRequests[(i * 9) % seed.SosRequests.Count] : null;
            var incident = new TeamIncident
            {
                MissionTeamId = team.Id,
                MissionActivityId = activity.Id,
                Location = OffsetPoint(activity.TargetLocation, 0.001 * (i % 3), -0.001 * (i % 2)),
                Description = IncidentDescription(i),
                Status = i % 3 == 0 ? "Resolved" : i % 3 == 1 ? "InProgress" : "Reported",
                IncidentScope = i % 2 == 0 ? "Activity" : "Mission",
                IncidentType = IncidentType(i),
                DecisionCode = i % 3 == 0 ? "COORDINATOR_REVIEWED" : null,
                DetailJson = Json(new { severity = i % 5 == 0 ? "High" : "Medium", weather = "mưa lớn", road = "ngập sâu" }),
                PayloadVersion = 1,
                NeedSupportSos = support is not null,
                NeedReassignActivity = i % 6 == 0,
                SupportSosRequestId = support?.Id,
                ReportedBy = seed.RescueTeamMembers.First(m => m.TeamId == team.RescuerTeamId).UserId,
                ReportedAt = activity.AssignedAt?.AddMinutes(45 + i)
            };
            _db.TeamIncidents.Add(incident);
            await _db.SaveChangesAsync(cancellationToken);
            _db.TeamIncidentActivities.Add(new TeamIncidentActivity
            {
                TeamIncidentId = incident.Id,
                MissionActivityId = activity.Id,
                OrderIndex = 1,
                IsPrimary = true
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedChatAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var sosByVictim = seed.SosRequests
            .Where(s => s.UserId.HasValue)
            .GroupBy(s => s.UserId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.CreatedAt).First());

        for (var i = 0; i < 140; i++)
        {
            var victim = seed.Victims[i];
            var sos = sosByVictim.GetValueOrDefault(victim.Id) ?? seed.SosRequests[i % seed.SosRequests.Count];
            var mission = seed.Missions.FirstOrDefault(m => m.ClusterId == sos.ClusterId);
            var status = i < 20 ? "AiAssist" : i < 50 ? "WaitingCoordinator" : i < 95 ? "CoordinatorActive" : "Closed";
            var conversationCreatedAt = ClampHistoricalUtc(
                (sos.CreatedAt ?? seed.StartUtc).AddMinutes(8),
                sos.CreatedAt ?? seed.StartUtc,
                seed.AnchorUtc);
            seed.Conversations.Add(new Conversation
            {
                VictimId = victim.Id,
                MissionId = i % 3 == 0 ? mission?.Id : null,
                Status = status,
                SelectedTopic = status == "AiAssist" ? "SosRequestSupport" : "Cần cập nhật ETA và vật phẩm",
                LinkedSosRequestId = sos.Id,
                CreatedAt = conversationCreatedAt,
                UpdatedAt = ClampHistoricalUtc(
                    conversationCreatedAt.AddHours(status == "Closed" ? 9 : 1),
                    conversationCreatedAt,
                    seed.AnchorUtc)
            });
        }

        _db.Conversations.AddRange(seed.Conversations);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var conversation in seed.Conversations)
        {
            var victim = seed.Victims.First(v => v.Id == conversation.VictimId);
            var coordinator = seed.Coordinators[conversation.Id % seed.Coordinators.Count];
            _db.ConversationParticipants.Add(new ConversationParticipant
            {
                ConversationId = conversation.Id,
                UserId = victim.Id,
                RoleInConversation = "Victim",
                JoinedAt = conversation.CreatedAt,
                LeftAt = conversation.Status == "Closed" ? conversation.UpdatedAt : null
            });
            _db.ConversationParticipants.Add(new ConversationParticipant
            {
                ConversationId = conversation.Id,
                UserId = coordinator.Id,
                RoleInConversation = "Coordinator",
                JoinedAt = ClampHistoricalUtc(
                    conversation.CreatedAt?.AddMinutes(conversation.Status == "WaitingCoordinator" ? 30 : 3),
                    conversation.CreatedAt ?? seed.StartUtc,
                    seed.AnchorUtc),
                LeftAt = conversation.Status == "Closed" ? conversation.UpdatedAt : null
            });
        }

        var messages = new List<Message>();
        for (var conversationIndex = 0; conversationIndex < seed.Conversations.Count; conversationIndex++)
        {
            var conversation = seed.Conversations[conversationIndex];
            var victim = seed.Victims.First(v => v.Id == conversation.VictimId);
            var coordinator = seed.Coordinators[conversationIndex % seed.Coordinators.Count];
            var count = 13 + (conversationIndex < 80 ? 1 : 0);
            for (var i = 0; i < count; i++)
            {
                var messageType = i == 1 ? "AiMessage" : i % 7 == 0 ? "SystemMessage" : "UserMessage";
                messages.Add(new Message
                {
                    ConversationId = conversation.Id,
                    SenderId = messageType == "SystemMessage" ? null : messageType == "AiMessage" ? null : i % 2 == 0 ? victim.Id : coordinator.Id,
                    Content = ChatMessage(i, conversation.Status),
                    MessageType = messageType,
                    CreatedAt = ClampHistoricalUtc(
                        (conversation.CreatedAt ?? seed.StartUtc).AddMinutes(i * 4),
                        conversation.CreatedAt ?? seed.StartUtc,
                        seed.AnchorUtc)
                });
            }
        }

        _db.Messages.AddRange(messages);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedSupplyRequestsAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var inProgressStatuses = new[]
        {
            ("Pending", "WaitingForApproval"),
            ("Accepted", "Approved"),
            ("Preparing", "Approved"),
            ("Shipping", "InTransit")
        };
        const int depotOneTwoRequestCount = 24;
        const int depotOneTwoIncompleteRequestCount = 12;
        var completedStatus = ("Completed", "Received");
        var completedOnlyDepots = seed.Depots
            .Skip(2)
            .Where(depot => !IsDepotClosureTestCandidate(depot))
            .ToList();

        for (var i = 0; i < 95; i++)
        {
            Depot requesting;
            Depot source;
            (string SourceStatus, string RequestingStatus) status;

            if (i < depotOneTwoRequestCount)
            {
                requesting = seed.Depots[i % 2];
                source = seed.Depots[(i + 1) % 2];
                status = i < depotOneTwoIncompleteRequestCount
                    ? inProgressStatuses[i % inProgressStatuses.Length]
                    : completedStatus;
            }
            else
            {
                var completedIndex = i - depotOneTwoRequestCount;
                requesting = completedOnlyDepots[completedIndex % completedOnlyDepots.Count];
                source = completedOnlyDepots[(completedIndex + 2) % completedOnlyDepots.Count];
                status = completedStatus;
            }

            var created = RandomEventUtc(seed, i + 220);
            var timeline = BuildSupplyRequestTimeline(created, status.SourceStatus, seed.AnchorUtc);
            var sourceManager = seed.Managers[(source.Id - 1) % seed.Managers.Count];
            var requestingManager = seed.Managers[(requesting.Id - 1) % seed.Managers.Count];
            seed.SupplyRequests.Add(new DepotSupplyRequest
            {
                RequestingDepotId = requesting.Id,
                SourceDepotId = source.Id,
                Note = SupplyRequestNote(i),
                PriorityLevel = status.SourceStatus == "Pending"
                    ? "Urgent"
                    : status.SourceStatus is "Accepted" or "Preparing" or "Shipping"
                        ? "High"
                        : i % 5 == 0 ? "High" : "Medium",
                SourceStatus = status.SourceStatus,
                RequestingStatus = status.RequestingStatus,
                RejectedReason = null,
                RequestedBy = requestingManager.Id,
                CreatedAt = created,
                AutoRejectAt = status.SourceStatus == "Pending" ? created.AddHours(i % 3 == 0 ? 2 : 6) : null,
                HighEscalationNotified = status.SourceStatus is "Accepted" or "Preparing" or "Shipping" or "Pending",
                HighEscalationNotifiedAt = timeline.HighEscalationNotifiedAt,
                UrgentEscalationNotified = status.SourceStatus == "Pending",
                UrgentEscalationNotifiedAt = timeline.UrgentEscalationNotifiedAt,
                RespondedAt = timeline.RespondedAt,
                ShippedAt = timeline.ShippedAt,
                CompletedAt = timeline.CompletedAt,
                UpdatedAt = timeline.UpdatedAt,
                AcceptedBy = status.SourceStatus is "Accepted" or "Preparing" or "Shipping" or "Completed" ? sourceManager.Id : null,
                RejectedBy = null,
                PreparedBy = status.SourceStatus is "Preparing" or "Shipping" or "Completed" ? sourceManager.Id : null,
                ShippedBy = status.SourceStatus is "Shipping" or "Completed" ? sourceManager.Id : null,
                CompletedBy = status.SourceStatus == "Completed" ? sourceManager.Id : null,
                ConfirmedBy = status.SourceStatus == "Completed" ? requestingManager.Id : null
            });
        }

        _db.DepotSupplyRequests.AddRange(seed.SupplyRequests);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var request in seed.SupplyRequests)
        {
            var itemCount = request.Id % 2 == 0 ? 3 : 2;
            for (var j = 0; j < itemCount; j++)
            {
                var item = seed.ItemModels[(request.Id * 3 + j) % seed.ItemModels.Count];
                _db.DepotSupplyRequestItems.Add(new DepotSupplyRequestItem
                {
                    DepotSupplyRequestId = request.Id,
                    ItemModelId = item.Id,
                    Quantity = item.ItemType == "Reusable" ? 2 + j : 60 + j * 40 + request.Id % 30
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static (
        DateTime? HighEscalationNotifiedAt,
        DateTime? UrgentEscalationNotifiedAt,
        DateTime? RespondedAt,
        DateTime? ShippedAt,
        DateTime? CompletedAt,
        DateTime UpdatedAt)
        BuildSupplyRequestTimeline(DateTime createdAt, string sourceStatus, DateTime anchorUtc)
    {
        DateTime? highEscalationNotifiedAt = sourceStatus is "Accepted" or "Preparing" or "Shipping" or "Pending"
            ? ClampHistoricalUtc(createdAt.AddMinutes(60), createdAt, anchorUtc)
            : null;
        DateTime? urgentEscalationNotifiedAt = sourceStatus == "Pending"
            ? ClampHistoricalUtc(createdAt.AddMinutes(25), createdAt, anchorUtc)
            : null;
        DateTime? respondedAt = sourceStatus == "Pending"
            ? null
            : ClampHistoricalUtc(createdAt.AddMinutes(30), createdAt, anchorUtc);
        DateTime? shippedAt = sourceStatus is "Shipping" or "Completed"
            ? ClampHistoricalUtc(createdAt.AddHours(3), respondedAt ?? createdAt, anchorUtc)
            : null;
        DateTime? completedAt = sourceStatus == "Completed"
            ? ClampHistoricalUtc(createdAt.AddHours(7), shippedAt ?? respondedAt ?? createdAt, anchorUtc)
            : null;
        var updatedAtCandidate = sourceStatus switch
        {
            "Completed" => createdAt.AddHours(7),
            "Shipping" => createdAt.AddHours(3),
            _ => createdAt.AddHours(1)
        };
        var updatedAtLowerBound = completedAt ?? shippedAt ?? respondedAt ?? highEscalationNotifiedAt ?? urgentEscalationNotifiedAt ?? createdAt;
        var updatedAt = ClampHistoricalUtc(updatedAtCandidate, updatedAtLowerBound, anchorUtc);
        return (highEscalationNotifiedAt, urgentEscalationNotifiedAt, respondedAt, shippedAt, completedAt, updatedAt);
    }

    private async Task SeedFinanceAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var systemFund = new SystemFund
        {
            Name = "Quỹ điều phối hệ thống",
            Balance = 4_500_000_000m,
            LastUpdatedAt = seed.AnchorUtc
        };
        _db.SystemFunds.Add(systemFund);

        var campaignPlans = new List<(FundCampaign Campaign, decimal PlannedRaised)>();
        var donationRatios = new[] { 22m, 18m, 15m, 13m, 11m, 9m, 7m, 5m };

        for (var i = 0; i < 11; i++)
        {
            var start = DateOnly.FromDateTime(seed.StartUtc.AddDays(120 + i * 75));
            var campaign = new FundCampaign
            {
                Code = $"FC-{start.Year}-B{i + 1:00}",
                Name = CampaignName(i),
                Region = "Huế - Đà Nẵng - Quảng Trị - Quảng Nam - Quảng Ngãi",
                CampaignStartDate = start,
                CampaignEndDate = start.AddDays(45),
                TargetAmount = 1_500_000_000m + i * 150_000_000m,
                // Calculated from seeded donation/disbursement history below.
                TotalAmount = 0m,
                CurrentBalance = 0m,
                Status = (i % 6 == 0 && i != 6) ? "Closed" : "Active",
                CreatedBy = seed.Admins[0].Id,
                CreatedAt = VnToUtc(start.ToDateTime(TimeOnly.MinValue)),
                LastModifiedBy = seed.Admins[0].Id,
                LastModifiedAt = seed.AnchorUtc.AddDays(-i),
                IsDeleted = false
            };

            seed.FundCampaigns.Add(campaign);
            campaignPlans.Add((campaign, 450_000_000m + i * 155_000_000m));
        }
        _db.FundCampaigns.AddRange(seed.FundCampaigns);
        await _db.SaveChangesAsync(cancellationToken);

        var donations = new List<Donation>();
        for (var campaignIndex = 0; campaignIndex < campaignPlans.Count; campaignIndex++)
        {
            var (campaign, plannedRaised) = campaignPlans[campaignIndex];
            var remaining = plannedRaised;
            var campaignStartLocal = campaign.CampaignStartDate!.Value.ToDateTime(new TimeOnly(8, 0));

            for (var donationIndex = 0; donationIndex < donationRatios.Length; donationIndex++)
            {
                var amount = donationIndex == donationRatios.Length - 1
                    ? remaining
                    : decimal.Round(plannedRaised * donationRatios[donationIndex] / 100m, 0, MidpointRounding.AwayFromZero);
                remaining -= amount;

                var donorSeed = campaignIndex * 17 + donationIndex;
                var (last, first) = VietnameseName(donorSeed);
                var donorName = donationIndex % 3 == 0
                    ? OrganizationName(donorSeed)
                    : $"{last} {first}";

                var orderId = $"{campaign.CampaignStartDate:yyMMdd}{campaign.Id:00}{donationIndex + 1:0000}";
                var paidAtLocal = campaignStartLocal
                    .AddDays(Math.Min(40, donationIndex * 5 + campaignIndex % 3))
                    .AddHours(donationIndex % 5);
                var paidAtUtc = VnToUtc(paidAtLocal);

                donations.Add(new Donation
                {
                    FundCampaignId = campaign.Id,
                    DonorName = donorName,
                    DonorEmail = $"donor-c{campaign.Id:00}-{donationIndex + 1:000}@resq.vn",
                    Amount = amount,
                    OrderId = orderId,
                    TransactionId = $"DEMO-TRX-{campaign.Id:00}-{donationIndex + 1:0000}",
                    Status = Status.Succeed.ToString(),
                    PaymentMethodCode = donationIndex % 2 == 0 ? PaymentMethodCode.PAYOS : PaymentMethodCode.MOMO,
                    PaidAt = paidAtUtc,
                    Note = "Đóng góp ủng hộ chiến dịch miền Trung.",
                    PaymentAuditInfo = donationIndex % 2 == 0
                        ? $"[PAYOS:order={orderId}]"
                        : $"[MOMO:campaign={campaign.Id},seq={donationIndex + 1}]",
                    IsPrivate = donationIndex % 4 == 1,
                    CreatedAt = paidAtUtc.AddMinutes(-10)
                });
            }
        }

        _db.Donations.AddRange(donations);
        await _db.SaveChangesAsync(cancellationToken);

        _db.FundTransactions.AddRange(donations.Select(donation => new FundTransaction
        {
            FundCampaignId = donation.FundCampaignId,
            Type = TransactionType.Donation.ToString(),
            Direction = "in",
            Amount = donation.Amount,
            ReferenceType = TransactionReferenceType.Donation.ToString(),
            ReferenceId = donation.Id,
            CreatedBy = null,
            CreatedAt = donation.PaidAt ?? donation.CreatedAt
        }));

        var depotFundCounts = new[] { 3, 2, 1, 3, 2, 1, 1 };
        var depotFundBalanceRatios = new[]
        {
            new[] { 0.50m, 0.30m, 0.20m },
            new[] { 0.65m, 0.35m },
            new[] { 1.00m }
        };
        foreach (var depot in seed.Depots)
        {
            var fundCount = depotFundCounts[(depot.Id - 1) % depotFundCounts.Length];
            var ratios = depotFundBalanceRatios[fundCount == 3 ? 0 : fundCount == 2 ? 1 : 2];
            var totalDepotBalance = 85_000_000 + depot.Id * 12_000_000;

            for (var fundIndex = 0; fundIndex < fundCount; fundIndex++)
            {
                var fundSourceType = fundIndex == 1
                    ? FundSourceType.SystemFund.ToString()
                    : FundSourceType.Campaign.ToString();
                var fundSourceId = fundSourceType == FundSourceType.SystemFund.ToString()
                    ? systemFund.Id
                    : seed.FundCampaigns[(depot.Id + fundIndex) % seed.FundCampaigns.Count].Id;

                _db.DepotFunds.Add(new DepotFund
                {
                    DepotId = depot.Id,
                    Balance = decimal.Round(totalDepotBalance * ratios[fundIndex], 0, MidpointRounding.AwayFromZero),
                    LastUpdatedAt = seed.AnchorUtc.AddHours(-fundIndex),
                    FundSourceType = fundSourceType,
                    FundSourceId = fundSourceId
                });
            }
        }
        await _db.SaveChangesAsync(cancellationToken);
        var depotFunds = await _db.DepotFunds.OrderBy(f => f.Id).ToListAsync(cancellationToken);

        var seededDisbursements = new List<CampaignDisbursement>();

        for (var i = 0; i < 42; i++)
        {
            var depot = seed.Depots[i % seed.Depots.Count];
            var approved = i < 30;
            var rejected = i >= 36;
            var created = RandomEventUtc(seed, i + 500);
            seed.FundingRequests.Add(new FundingRequest
            {
                DepotId = depot.Id,
                RequestedBy = seed.Managers[i % seed.Managers.Count].Id,
                TotalAmount = 12_000_000 + (i % 10) * 4_500_000,
                Description = "Bổ sung thuốc, áo mưa, nước uống và vật tư vệ sinh cho đợt mưa lũ",
                AttachmentUrl = $"https://cdn.resq.vn/funding/fr-{i + 1:000}.xlsx",
                Status = approved ? "Approved" : rejected ? "Rejected" : "Pending",
                ApprovedCampaignId = approved ? seed.FundCampaigns[i % seed.FundCampaigns.Count].Id : null,
                ReviewedBy = approved || rejected ? seed.Admins[0].Id : null,
                ReviewedAt = approved || rejected ? created.AddHours(6) : null,
                RejectionReason = rejected ? "Chưa đủ báo giá kèm theo" : null,
                CreatedAt = created
            });
        }
        _db.FundingRequests.AddRange(seed.FundingRequests);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var request in seed.FundingRequests)
        {
            for (var j = 0; j < 4; j++)
            {
                var item = seed.ItemModels[(request.Id * 5 + j) % seed.ItemModels.Count];
                var unitPrice = item.ItemType == "Reusable" ? 350_000 + j * 120_000 : 18_000 + j * 7_000;
                var quantity = item.ItemType == "Reusable" ? 3 + j : 50 + j * 20;
                _db.FundingRequestItems.Add(new FundingRequestItem
                {
                    FundingRequestId = request.Id,
                    Row = j + 1,
                    ItemName = item.Name ?? "Vật phẩm cứu trợ",
                    CategoryCode = seed.Categories.First(c => c.Id == item.CategoryId).Code ?? "GENERAL",
                    Unit = item.Unit,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    TotalPrice = unitPrice * quantity,
                    ItemType = item.ItemType ?? "Consumable",
                    TargetGroup = "Adult",
                    VolumePerUnit = item.VolumePerUnit ?? 0,
                    WeightPerUnit = item.WeightPerUnit ?? 0,
                    ReceivedDate = DateOnly.FromDateTime(request.CreatedAt),
                    ExpiredDate = item.ItemType == "Consumable" ? DateOnly.FromDateTime(request.CreatedAt.AddMonths(8)) : null,
                    Notes = "Dòng seed demo cho funding request"
                });
            }
        }

        foreach (var request in seed.FundingRequests.Where(r => r.Status == "Approved").Take(25))
        {
            var disbursement = new CampaignDisbursement
            {
                FundCampaignId = request.ApprovedCampaignId!.Value,
                DepotId = request.DepotId,
                Amount = request.TotalAmount,
                Purpose = $"Duyệt yêu cầu cấp quỹ #{request.Id}",
                Type = "FundingRequestApproval",
                FundingRequestId = request.Id,
                CreatedBy = seed.Admins[0].Id,
                CreatedAt = request.ReviewedAt ?? request.CreatedAt.AddHours(8)
            };
            _db.CampaignDisbursements.Add(disbursement);
            await _db.SaveChangesAsync(cancellationToken);
            seededDisbursements.Add(disbursement);

            for (var j = 0; j < 3; j++)
            {
                var item = seed.ItemModels[(request.Id + j) % seed.ItemModels.Count];
                var unitPrice = item.ItemType == "Reusable" ? 420_000 : 25_000;
                var quantity = item.ItemType == "Reusable" ? 2 + j : 60 + j * 40;
                _db.DisbursementItems.Add(new DisbursementItem
                {
                    CampaignDisbursementId = disbursement.Id,
                    ItemName = item.Name ?? "Vật phẩm",
                    Unit = item.Unit,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    TotalPrice = unitPrice * quantity,
                    Note = "Mua theo kế hoạch giải ngân",
                    CreatedAt = disbursement.CreatedAt
                });
            }
        }

        var adminDisbursements = seed.FundCampaigns
            .Select((campaign, index) => new CampaignDisbursement
            {
                FundCampaignId = campaign.Id,
                DepotId = seed.Depots[(index * 2) % seed.Depots.Count].Id,
                Amount = 18_000_000m + (index % 5) * 4_000_000m,
                Purpose = "Admin chủ động cấp tiền cho kho theo kế hoạch dự phòng",
                Type = DisbursementType.AdminAllocation.ToString(),
                FundingRequestId = null,
                CreatedBy = seed.Admins[0].Id,
                CreatedAt = VnToUtc(campaign.CampaignStartDate!.Value.ToDateTime(new TimeOnly(10, 0)).AddDays(28 + index % 5))
            })
            .ToList();

        _db.CampaignDisbursements.AddRange(adminDisbursements);
        seededDisbursements.AddRange(adminDisbursements);
        await _db.SaveChangesAsync(cancellationToken);

        _db.FundTransactions.AddRange(seededDisbursements.Select(disbursement => new FundTransaction
        {
            FundCampaignId = disbursement.FundCampaignId,
            Type = TransactionType.Allocation.ToString(),
            Direction = "out",
            Amount = disbursement.Amount,
            ReferenceType = TransactionReferenceType.CampaignDisbursement.ToString(),
            ReferenceId = disbursement.Id,
            CreatedBy = disbursement.CreatedBy,
            CreatedAt = disbursement.CreatedAt
        }));

        var raisedByCampaign = donations
            .Where(d => d.FundCampaignId.HasValue && d.Amount.HasValue && d.Status == Status.Succeed.ToString())
            .GroupBy(d => d.FundCampaignId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount ?? 0m));

        var disbursedByCampaign = seededDisbursements
            .GroupBy(d => d.FundCampaignId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        foreach (var campaign in seed.FundCampaigns)
        {
            var totalRaised = raisedByCampaign.TryGetValue(campaign.Id, out var raised) ? raised : 0m;
            var totalDisbursed = disbursedByCampaign.TryGetValue(campaign.Id, out var disbursed) ? disbursed : 0m;

            campaign.TotalAmount = totalRaised;
            campaign.CurrentBalance = totalRaised - totalDisbursed;
        }

        var vatInvoices = await _db.VatInvoices.OrderBy(v => v.Id).ToListAsync(cancellationToken);

        // Calculate LiquidationRevenue needed to support all allocations from SystemFund
        decimal totalSystemFundNeeded = 0m;
        foreach (var fund in depotFunds)
        {
            if (fund.FundSourceType == FundSourceType.SystemFund.ToString())
            {
                totalSystemFundNeeded += 25_000_000m + fund.Id * 5_000_000m;
            }
        }

        var systemFundCreatedAt = seed.StartUtc.AddDays(200);
        if (totalSystemFundNeeded > 0)
        {
            var initialRevenue = totalSystemFundNeeded + 100_000_000m;
            _db.SystemFundTransactions.Add(new SystemFundTransaction
            {
                SystemFundId = systemFund.Id,
                TransactionType = SystemFundTransactionType.LiquidationRevenue.ToString(),
                Amount = initialRevenue,
                ReferenceType = "DepotClosure",
                ReferenceId = 0,
                Note = "Nguồn thu thanh lý tài sản đầu kỳ",
                CreatedBy = seed.Admins[0].Id,
                CreatedAt = systemFundCreatedAt
            });
            systemFund.Balance += initialRevenue;
        }

        foreach (var fund in depotFunds)
        {
            var managerId = seed.Managers[fund.DepotId % seed.Managers.Count].Id;
            var fundCreatedAt = seed.StartUtc.AddDays(220 + fund.Id * 3);
            fund.Balance = 0; // Reset for recalculation

            // 1. Allocation
            decimal allocationAmount = 25_000_000m + fund.Id * 5_000_000m;
            int? allocationRefId = null;
            string allocationRefType = "";

            if (fund.FundSourceType == FundSourceType.SystemFund.ToString())
            {
                allocationRefType = "SystemFund";
                allocationRefId = systemFund.Id;

                _db.SystemFundTransactions.Add(new SystemFundTransaction
                {
                    SystemFundId = systemFund.Id,
                    TransactionType = SystemFundTransactionType.AllocationToDepot.ToString(),
                    Amount = allocationAmount,
                    ReferenceType = "SystemFund",
                    ReferenceId = fund.Id,
                    Note = $"Cấp vốn cho quỹ kho {fund.DepotId}",
                    CreatedBy = seed.Admins[0].Id,
                    CreatedAt = fundCreatedAt
                });
                systemFund.Balance -= allocationAmount;
            }
            else
            {
                allocationRefType = DepotFundReferenceType.CampaignDisbursement.ToString();
                var disbursement = seededDisbursements.FirstOrDefault(d => d.DepotId == fund.DepotId && d.FundCampaignId == fund.FundSourceId);
                if (disbursement != null)
                {
                    allocationRefId = disbursement.Id;
                    allocationAmount = disbursement.Amount;
                }
                else if (seededDisbursements.Count > 0)
                {
                    allocationRefId = seededDisbursements[0].Id;
                }
                else
                {
                    allocationRefId = 1;
                }
            }

            _db.DepotFundTransactions.Add(new DepotFundTransaction
            {
                DepotFundId = fund.Id,
                TransactionType = DepotFundTransactionType.Allocation.ToString(),
                Amount = allocationAmount,
                ReferenceType = allocationRefType,
                ReferenceId = allocationRefId,
                Note = "Nhận giải ngân vào quỹ kho",
                CreatedBy = seed.Admins[0].Id,
                CreatedAt = fundCreatedAt
            });
            fund.Balance += allocationAmount;

            // 2. Personal Advance
            var advanceAmount = 5_000_000m + fund.Id * 1_000_000m;
            _db.DepotFundTransactions.Add(new DepotFundTransaction
            {
                DepotFundId = fund.Id,
                TransactionType = DepotFundTransactionType.PersonalAdvance.ToString(),
                Amount = advanceAmount,
                ReferenceType = "InternalAdvance",
                ReferenceId = fund.DepotId,
                Note = "Cá nhân ứng trước cho kho khi cần nhập hàng nhanh",
                CreatedBy = managerId,
                ContributorName = FullName(seed.Managers[fund.DepotId % seed.Managers.Count]),
                ContributorPhoneNumber = seed.Managers[fund.DepotId % seed.Managers.Count].Phone,
                ContributorId = managerId,
                CreatedAt = fundCreatedAt.AddHours(2)
            });
            fund.Balance += advanceAmount;

            // 3. Deduction (VatInvoice)
            var invoice = vatInvoices.Skip(fund.Id % Math.Max(1, vatInvoices.Count)).FirstOrDefault() ?? vatInvoices.FirstOrDefault();
            if (invoice != null)
            {
                var deductionAmount = (invoice.TotalAmount ?? 0m) > 0 ? invoice.TotalAmount.Value : 1_500_000m;
                if (fund.Balance >= deductionAmount)
                {
                    _db.DepotFundTransactions.Add(new DepotFundTransaction
                    {
                        DepotFundId = fund.Id,
                        TransactionType = DepotFundTransactionType.Deduction.ToString(),
                        Amount = deductionAmount,
                        ReferenceType = DepotFundReferenceType.VatInvoice.ToString(),
                        ReferenceId = invoice.Id,
                        Note = "Thanh toán mua bổ sung hàng cứu trợ từ quỹ kho",
                        CreatedBy = managerId,
                        CreatedAt = fundCreatedAt.AddHours(5)
                    });
                    fund.Balance -= deductionAmount;
                }
            }

            // 4. Advance Repayment
            var repaymentAmount = advanceAmount / 2;
            if (fund.Balance >= repaymentAmount)
            {
                _db.DepotFundTransactions.Add(new DepotFundTransaction
                {
                    DepotFundId = fund.Id,
                    TransactionType = DepotFundTransactionType.AdvanceRepayment.ToString(),
                    Amount = repaymentAmount,
                    ReferenceType = "InternalRepayment",
                    ReferenceId = fund.DepotId,
                    Note = "Kho hoàn trả một phần tiền ứng cá nhân",
                    CreatedBy = managerId,
                    ContributorName = FullName(seed.Managers[fund.DepotId % seed.Managers.Count]),
                    ContributorPhoneNumber = seed.Managers[fund.DepotId % seed.Managers.Count].Phone,
                    ContributorId = managerId,
                    CreatedAt = fundCreatedAt.AddHours(24)
                });
                fund.Balance -= repaymentAmount;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedAuditAndHistoryAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        await SeedInventoryMovementHistoryAsync(seed, cancellationToken);

        foreach (var depot in seed.Depots)
        {
            _db.InventoryStockThresholdConfigs.Add(new InventoryStockThresholdConfig
            {
                ScopeType = "DEPOT",
                DepotId = depot.Id,
                DangerRatio = 0.18m,
                WarningRatio = 0.35m,
                MinimumThreshold = 120,
                IsActive = true,
                UpdatedBy = seed.Managers[depot.Id % seed.Managers.Count].Id,
                UpdatedAt = seed.AnchorUtc.AddDays(-depot.Id),
                RowVersion = 1
            });
        }

        foreach (var category in seed.Categories.Take(20))
        {
            var depot = seed.Depots[category.Id % seed.Depots.Count];
            _db.InventoryStockThresholdConfigs.Add(new InventoryStockThresholdConfig
            {
                ScopeType = "DEPOT_CATEGORY",
                DepotId = depot.Id,
                CategoryId = category.Id,
                DangerRatio = 0.15m,
                WarningRatio = 0.32m,
                MinimumThreshold = 200,
                IsActive = true,
                UpdatedBy = seed.Managers[category.Id % seed.Managers.Count].Id,
                UpdatedAt = seed.AnchorUtc.AddDays(-category.Id),
                RowVersion = 1
            });
        }

        foreach (var item in seed.ItemModels.Take(30))
        {
            var depot = seed.Depots[item.Id % seed.Depots.Count];
            _db.InventoryStockThresholdConfigs.Add(new InventoryStockThresholdConfig
            {
                ScopeType = "DEPOT_ITEM",
                DepotId = depot.Id,
                ItemModelId = item.Id,
                DangerRatio = 0.12m,
                WarningRatio = 0.30m,
                MinimumThreshold = item.ItemType == "Reusable" ? 3 : 80,
                IsActive = true,
                UpdatedBy = seed.Managers[item.Id % seed.Managers.Count].Id,
                UpdatedAt = seed.AnchorUtc.AddDays(-item.Id % 60),
                RowVersion = 1
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        var configs = await _db.InventoryStockThresholdConfigs
            .Where(c => c.Id != 1)
            .OrderBy(c => c.Id)
            .Take(90)
            .ToListAsync(cancellationToken);
        foreach (var config in configs)
        {
            _db.InventoryStockThresholdConfigHistories.Add(new InventoryStockThresholdConfigHistory
            {
                ConfigId = config.Id,
                ScopeType = config.ScopeType,
                DepotId = config.DepotId,
                CategoryId = config.CategoryId,
                ItemModelId = config.ItemModelId,
                OldDangerRatio = 0.10m,
                NewDangerRatio = config.DangerRatio,
                OldWarningRatio = 0.25m,
                NewWarningRatio = config.WarningRatio,
                ChangedBy = seed.Managers[config.Id % seed.Managers.Count].Id,
                ChangedAt = seed.AnchorUtc.AddDays(-config.Id % 90),
                ChangeReason = "Mùa mưa bão cần mức dự trữ cao hơn",
                Action = "Update"
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedInventoryMovementHistoryAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var vatInvoices = await _db.VatInvoices
            .OrderBy(v => v.Id)
            .ToListAsync(cancellationToken);
        var requestItems = await _db.DepotSupplyRequestItems
            .AsNoTracking()
            .OrderBy(i => i.Id)
            .ToListAsync(cancellationToken);
        var missionItems = await _db.MissionItems
            .AsNoTracking()
            .OrderBy(i => i.Id)
            .ToListAsync(cancellationToken);

        var vatInvoiceIds = vatInvoices.Select(v => v.Id).ToArray();
        var itemModelsById = seed.ItemModels.ToDictionary(i => i.Id);
        var missionsById = seed.Missions.ToDictionary(m => m.Id);
        var lotsByInventoryId = seed.Lots
            .GroupBy(l => l.SupplyInventoryId)
            .ToDictionary(g => g.Key, g => g.OrderBy(l => l.Id).ToList());
        var inventoriesByDepotItem = seed.Inventories
            .Where(i => i.DepotId.HasValue && i.ItemModelId.HasValue)
            .ToDictionary(i => (i.DepotId!.Value, i.ItemModelId!.Value));

        var consumablePlans = seed.Inventories
            .Where(i => i.DepotId.HasValue
                && i.ItemModelId.HasValue
                && itemModelsById.TryGetValue(i.ItemModelId.Value, out var itemModel)
                && string.Equals(itemModel.ItemType, "Consumable", StringComparison.Ordinal)
                && lotsByInventoryId.ContainsKey(i.Id))
            .Select(i =>
            {
                var seedImportLots = lotsByInventoryId[i.Id];
                return new ConsumableInventoryHistoryPlan
                {
                    Inventory = i,
                    ItemModel = itemModelsById[i.ItemModelId!.Value],
                    BaseLot = seedImportLots[0],
                    SupplementalImportLots = seedImportLots.Skip(1).ToList(),
                    PerformedBy = ManagerForDepot(seed, i.DepotId!.Value)
                };
            })
            .ToDictionary(plan => plan.Inventory.Id);

        var transferLogCount = BuildCompletedTransferHistory(
            seed,
            requestItems,
            itemModelsById,
            inventoriesByDepotItem,
            consumablePlans);

        var missionExportTarget = 100 - transferLogCount;
        BuildMissionExportHistory(
            seed,
            missionItems,
            itemModelsById,
            missionsById,
            inventoriesByDepotItem,
            consumablePlans,
            missionExportTarget);

        BuildAdjustmentHistory(consumablePlans.Values.ToList(), seed.AnchorUtc);

        var inventoryLogs = new List<InventoryLog>(820);
        BuildConsumableInventoryHistory(seed, vatInvoiceIds, consumablePlans.Values.ToList(), inventoryLogs);
        BuildReusableInventoryHistory(seed, vatInvoiceIds, inventoryLogs);

        _db.InventoryLogs.AddRange(inventoryLogs);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private int BuildCompletedTransferHistory(
        DemoSeedContext seed,
        IReadOnlyList<DepotSupplyRequestItem> requestItems,
        IReadOnlyDictionary<int, ItemModel> itemModelsById,
        IReadOnlyDictionary<(int DepotId, int ItemModelId), SupplyInventory> inventoriesByDepotItem,
        IReadOnlyDictionary<int, ConsumableInventoryHistoryPlan> consumablePlans)
    {
        var requestItemsByRequestId = requestItems
            .GroupBy(i => i.DepotSupplyRequestId)
            .ToDictionary(g => g.Key, g => g.OrderBy(i => i.Id).ToList());
        var inboundCapacity = consumablePlans.Values.ToDictionary(plan => plan.Inventory.Id, plan => plan.FinalQuantity);
        var transferLogs = 0;

        foreach (var request in seed.SupplyRequests
                     .Where(r => string.Equals(r.SourceStatus, "Completed", StringComparison.Ordinal))
                     .OrderBy(r => r.Id))
        {
            if (transferLogs >= 100 || !requestItemsByRequestId.TryGetValue(request.Id, out var items))
            {
                continue;
            }

            foreach (var item in items)
            {
                if (transferLogs >= 100
                    || !itemModelsById.TryGetValue(item.ItemModelId, out var itemModel)
                    || !string.Equals(itemModel.ItemType, "Consumable", StringComparison.Ordinal)
                    || !inventoriesByDepotItem.TryGetValue((request.SourceDepotId, item.ItemModelId), out var sourceInventory)
                    || !inventoriesByDepotItem.TryGetValue((request.RequestingDepotId, item.ItemModelId), out var destinationInventory)
                    || !consumablePlans.TryGetValue(sourceInventory.Id, out var sourcePlan)
                    || !consumablePlans.TryGetValue(destinationInventory.Id, out var destinationPlan))
                {
                    continue;
                }

                var remainingInboundCapacity = inboundCapacity[destinationPlan.Inventory.Id];
                var quantity = Math.Min(item.Quantity, 10 + item.Id % 18);
                quantity = Math.Min(quantity, Math.Max(0, Math.Min(32, remainingInboundCapacity / 4)));
                if (quantity < 6)
                {
                    continue;
                }

                var shippedAt = ClampHistoricalUtc(
                    request.ShippedAt ?? request.CompletedAt ?? request.CreatedAt.AddHours(3),
                    request.CreatedAt,
                    seed.AnchorUtc);
                var completedAt = ClampHistoricalUtc(
                    request.CompletedAt ?? shippedAt.AddHours(4),
                    shippedAt,
                    seed.AnchorUtc);

                sourcePlan.OutboundEvents.Add(new ConsumableOutboundEvent
                {
                    ActionType = InventoryActionType.TransferOut.ToString(),
                    SourceType = InventorySourceType.Transfer.ToString(),
                    SourceId = request.Id,
                    Quantity = quantity,
                    CreatedAt = shippedAt,
                    PerformedBy = request.ShippedBy ?? request.PreparedBy ?? sourcePlan.PerformedBy,
                    MissionId = null,
                    Note = $"Xuất chuyển {itemModel.Name} từ {request.SourceDepot?.Name ?? $"kho #{request.SourceDepotId}"} sang {request.RequestingDepot?.Name ?? $"kho #{request.RequestingDepotId}"} theo phiếu #{request.Id}"
                });

                destinationPlan.InboundTransfers.Add(new ConsumableInboundTransferEvent
                {
                    Quantity = quantity,
                    SourceId = request.Id,
                    CreatedAt = completedAt,
                    PerformedBy = request.ConfirmedBy ?? request.CompletedBy ?? destinationPlan.PerformedBy,
                    ReceivedDate = completedAt,
                    ExpiredDate = sourcePlan.BaseLot.ExpiredDate,
                    Note = $"Nhận chuyển {itemModel.Name} tại {request.RequestingDepot?.Name ?? $"kho #{request.RequestingDepotId}"} từ phiếu điều phối #{request.Id}"
                });

                inboundCapacity[destinationPlan.Inventory.Id] -= quantity;
                transferLogs += 2;
            }
        }

        return transferLogs;
    }

    private void BuildMissionExportHistory(
        DemoSeedContext seed,
        IReadOnlyList<MissionItem> missionItems,
        IReadOnlyDictionary<int, ItemModel> itemModelsById,
        IReadOnlyDictionary<int, Mission> missionsById,
        IReadOnlyDictionary<(int DepotId, int ItemModelId), SupplyInventory> inventoriesByDepotItem,
        IReadOnlyDictionary<int, ConsumableInventoryHistoryPlan> consumablePlans,
        int missionExportTarget)
    {
        if (missionExportTarget <= 0)
        {
            return;
        }

        var added = 0;
        foreach (var missionItem in missionItems)
        {
            if (added >= missionExportTarget
                || missionItem.SourceDepotId is null
                || missionItem.ItemModelId is null
                || !itemModelsById.TryGetValue(missionItem.ItemModelId.Value, out var itemModel)
                || !string.Equals(itemModel.ItemType, "Consumable", StringComparison.Ordinal)
                || !inventoriesByDepotItem.TryGetValue((missionItem.SourceDepotId.Value, missionItem.ItemModelId.Value), out var inventory)
                || !consumablePlans.TryGetValue(inventory.Id, out var plan)
                || missionItem.MissionId is null
                || !missionsById.TryGetValue(missionItem.MissionId.Value, out var mission)
                || string.Equals(mission.Status, "Planned", StringComparison.Ordinal))
            {
                continue;
            }

            var quantity = missionItem.AllocatedQuantity ?? missionItem.RequiredQuantity ?? 0;
            quantity = Math.Min(quantity, 14 + missionItem.Id % 24);
            if (quantity <= 0)
            {
                continue;
            }

            plan.OutboundEvents.Add(new ConsumableOutboundEvent
            {
                ActionType = InventoryActionType.Export.ToString(),
                SourceType = InventorySourceType.Mission.ToString(),
                SourceId = mission.Id,
                Quantity = quantity,
                CreatedAt = (mission.StartTime ?? mission.CreatedAt ?? seed.StartUtc).AddMinutes(25 + missionItem.Id % 40),
                PerformedBy = plan.PerformedBy,
                MissionId = mission.Id,
                Note = $"Xuất {itemModel.Name} cho mission #{mission.Id} thuộc cụm SOS #{mission.ClusterId}"
            });
            added++;
        }
    }

    private static void BuildAdjustmentHistory(IReadOnlyList<ConsumableInventoryHistoryPlan> plans, DateTime anchorUtc)
    {
        foreach (var plan in plans
                     .OrderBy(p => p.Inventory.Id)
                     .Where(p => p.Inventory.Id % 3 == 0)
                     .Take(45))
        {
            var quantity = Math.Min(3 + plan.Inventory.Id % 8, Math.Max(2, Math.Max(1, plan.FinalQuantity / 30)));
            var baseCreatedAt = plan.Inventory.LastStockedAt ?? plan.BaseLot.ReceivedDate ?? anchorUtc.AddDays(-90);
            var createdAtCandidate = baseCreatedAt.AddDays(18 + plan.Inventory.Id % 40);
            var fallbackCreatedAt = TrimUtcToMinute(anchorUtc.AddHours(-(6 + plan.Inventory.Id % 96)));
            plan.Adjustments.Add(new ConsumableAdjustmentEvent
            {
                Quantity = quantity,
                CreatedAt = createdAtCandidate <= anchorUtc
                    ? createdAtCandidate
                    : ClampHistoricalUtc(fallbackCreatedAt, baseCreatedAt, anchorUtc),
                PerformedBy = plan.PerformedBy,
                Note = $"Điều chỉnh giảm {plan.ItemModel.Name} sau kiểm kê do hư hỏng hoặc quá hạn"
            });
        }
    }

    private void BuildConsumableInventoryHistory(
        DemoSeedContext seed,
        IReadOnlyList<int> vatInvoiceIds,
        IReadOnlyList<ConsumableInventoryHistoryPlan> plans,
        ICollection<InventoryLog> inventoryLogs)
    {
        foreach (var plan in plans.OrderBy(p => p.Inventory.Id))
        {
            var inboundQuantity = plan.InboundTransfers.Sum(t => t.Quantity);
            var outboundQuantity = plan.OutboundEvents.Sum(t => t.Quantity) + plan.Adjustments.Sum(t => t.Quantity);
            var supplementalImportQuantity = plan.SupplementalImportLots.Sum(lot => lot.RemainingQuantity);
            var baseRemaining = plan.FinalQuantity - inboundQuantity - supplementalImportQuantity;
            if (baseRemaining < 0)
            {
                throw new InvalidOperationException(
                    $"Consumable inventory #{plan.Inventory.Id} cannot fit supplemental seed lots into final quantity.");
            }

            var baseQuantity = Math.Max(1, baseRemaining + outboundQuantity);
            var receivedDate = plan.BaseLot.ReceivedDate ?? seed.StartUtc.AddDays(120 + plan.Inventory.Id % 520);
            var expiredDate = plan.BaseLot.ExpiredDate ?? receivedDate.AddMonths(6 + plan.Inventory.Id % 15);
            var sourceType = string.Equals(plan.BaseLot.SourceType, InventorySourceType.Purchase.ToString(), StringComparison.Ordinal)
                ? InventorySourceType.Purchase.ToString()
                : InventorySourceType.Donation.ToString();
            var sourceId = plan.BaseLot.SourceId ?? plan.Inventory.Id;

            plan.BaseLot.Quantity = baseQuantity;
            plan.BaseLot.RemainingQuantity = baseRemaining;
            plan.BaseLot.ReceivedDate = receivedDate;
            plan.BaseLot.ExpiredDate = expiredDate;
            plan.BaseLot.SourceType = sourceType;
            plan.BaseLot.SourceId = sourceId;
            plan.BaseLot.CreatedAt = receivedDate;
            var latestSeededReceipt = plan.SupplementalImportLots
                .Select(lot => lot.ReceivedDate ?? lot.CreatedAt)
                .Append(receivedDate)
                .Max();
            plan.Inventory.LastStockedAt = plan.InboundTransfers.Count == 0
                ? latestSeededReceipt
                : new[] { latestSeededReceipt, plan.InboundTransfers.Max(t => t.CreatedAt) }.Max();

            inventoryLogs.Add(new InventoryLog
            {
                DepotSupplyInventoryId = plan.Inventory.Id,
                SupplyInventoryLot = plan.BaseLot,
                VatInvoiceId = ResolveVatInvoiceId(vatInvoiceIds, sourceType, sourceId),
                ActionType = InventoryActionType.Import.ToString(),
                QuantityChange = baseQuantity,
                SourceType = sourceType,
                SourceId = sourceId,
                PerformedBy = plan.PerformedBy,
                Note = $"Nhập gốc {plan.ItemModel.Name} vào {plan.Inventory.Depot?.Name ?? $"kho #{plan.Inventory.DepotId}"}",
                ReceivedDate = receivedDate,
                ExpiredDate = expiredDate,
                CreatedAt = receivedDate
            });

            foreach (var supplementalLot in plan.SupplementalImportLots.OrderBy(lot => lot.CreatedAt).ThenBy(lot => lot.SourceId))
            {
                var supplementalReceivedDate = supplementalLot.ReceivedDate ?? supplementalLot.CreatedAt;
                var supplementalExpiredDate = supplementalLot.ExpiredDate;
                var supplementalSourceType = string.Equals(supplementalLot.SourceType, InventorySourceType.Purchase.ToString(), StringComparison.Ordinal)
                    ? InventorySourceType.Purchase.ToString()
                    : InventorySourceType.Donation.ToString();
                var supplementalSourceId = supplementalLot.SourceId ?? plan.Inventory.Id;

                supplementalLot.SourceType = supplementalSourceType;
                supplementalLot.SourceId = supplementalSourceId;
                supplementalLot.ReceivedDate = supplementalReceivedDate;
                supplementalLot.CreatedAt = supplementalReceivedDate;

                inventoryLogs.Add(new InventoryLog
                {
                    DepotSupplyInventoryId = plan.Inventory.Id,
                    SupplyInventoryLot = supplementalLot,
                    ActionType = InventoryActionType.Import.ToString(),
                    QuantityChange = supplementalLot.Quantity,
                    SourceType = supplementalSourceType,
                    SourceId = supplementalSourceId,
                    PerformedBy = plan.PerformedBy,
                    Note = $"Nhập lô demo sắp hết hạn {plan.ItemModel.Name} vào {plan.Inventory.Depot?.Name ?? $"kho #{plan.Inventory.DepotId}"}",
                    ReceivedDate = supplementalReceivedDate,
                    ExpiredDate = supplementalExpiredDate,
                    CreatedAt = supplementalReceivedDate
                });
            }

            foreach (var outbound in plan.OutboundEvents.OrderBy(e => e.CreatedAt))
            {
                inventoryLogs.Add(new InventoryLog
                {
                    DepotSupplyInventoryId = plan.Inventory.Id,
                    SupplyInventoryLot = plan.BaseLot,
                    ActionType = outbound.ActionType,
                    QuantityChange = outbound.Quantity,
                    SourceType = outbound.SourceType,
                    SourceId = outbound.SourceId,
                    MissionId = outbound.MissionId,
                    PerformedBy = outbound.PerformedBy,
                    Note = outbound.Note,
                    ReceivedDate = plan.BaseLot.ReceivedDate,
                    ExpiredDate = plan.BaseLot.ExpiredDate,
                    CreatedAt = outbound.CreatedAt
                });
            }

            foreach (var adjustment in plan.Adjustments.OrderBy(a => a.CreatedAt))
            {
                inventoryLogs.Add(new InventoryLog
                {
                    DepotSupplyInventoryId = plan.Inventory.Id,
                    SupplyInventoryLot = plan.BaseLot,
                    ActionType = InventoryActionType.Adjust.ToString(),
                    QuantityChange = -adjustment.Quantity,
                    SourceType = InventorySourceType.Adjustment.ToString(),
                    PerformedBy = adjustment.PerformedBy,
                    Note = adjustment.Note,
                    ReceivedDate = plan.BaseLot.ReceivedDate,
                    ExpiredDate = plan.BaseLot.ExpiredDate,
                    CreatedAt = adjustment.CreatedAt
                });
            }

            foreach (var inbound in plan.InboundTransfers.OrderBy(t => t.CreatedAt))
            {
                var transferLot = new SupplyInventoryLot
                {
                    SupplyInventoryId = plan.Inventory.Id,
                    Quantity = inbound.Quantity,
                    RemainingQuantity = inbound.Quantity,
                    ReceivedDate = inbound.ReceivedDate,
                    ExpiredDate = inbound.ExpiredDate,
                    SourceType = InventorySourceType.Transfer.ToString(),
                    SourceId = inbound.SourceId,
                    CreatedAt = inbound.CreatedAt
                };

                seed.Lots.Add(transferLot);
                _db.SupplyInventoryLots.Add(transferLot);

                inventoryLogs.Add(new InventoryLog
                {
                    DepotSupplyInventoryId = plan.Inventory.Id,
                    SupplyInventoryLot = transferLot,
                    ActionType = InventoryActionType.TransferIn.ToString(),
                    QuantityChange = inbound.Quantity,
                    SourceType = InventorySourceType.Transfer.ToString(),
                    SourceId = inbound.SourceId,
                    PerformedBy = inbound.PerformedBy,
                    Note = inbound.Note,
                    ReceivedDate = inbound.ReceivedDate,
                    ExpiredDate = inbound.ExpiredDate,
                    CreatedAt = inbound.CreatedAt
                });
            }
        }
    }

    private void BuildReusableInventoryHistory(
        DemoSeedContext seed,
        IReadOnlyList<int> vatInvoiceIds,
        ICollection<InventoryLog> inventoryLogs)
    {
        foreach (var reusableItem in seed.ReusableItems.OrderBy(item => item.Id))
        {
            var sourceType = reusableItem.Id % 3 == 0
                ? InventorySourceType.Purchase.ToString()
                : InventorySourceType.Donation.ToString();
            var sourceId = reusableItem.Id % 3 == 0
                ? vatInvoiceIds[(reusableItem.Id - 1) % vatInvoiceIds.Count]
                : reusableItem.Id;
            var createdAt = reusableItem.CreatedAt ?? seed.StartUtc.AddDays(140 + reusableItem.Id % 480);

            inventoryLogs.Add(new InventoryLog
            {
                ReusableItemId = reusableItem.Id,
                VatInvoiceId = sourceType == InventorySourceType.Purchase.ToString()
                    ? sourceId
                    : null,
                ActionType = InventoryActionType.Import.ToString(),
                QuantityChange = 1,
                SourceType = sourceType,
                SourceId = sourceId,
                PerformedBy = ManagerForDepot(seed, reusableItem.DepotId ?? seed.Depots[reusableItem.Id % seed.Depots.Count].Id),
                Note = $"Nhập thiết bị {reusableItem.ItemModel?.Name ?? $"vật phẩm #{reusableItem.ItemModelId}"} vào kho ban đầu",
                ReceivedDate = createdAt,
                CreatedAt = createdAt
            });
        }

        var reusableMissionUnits = seed.ReusableItems
            .Where(item => item.DepotId.HasValue && !string.Equals(item.Status, "Maintenance", StringComparison.Ordinal))
            .OrderBy(item => item.Id)
            .Take(30)
            .ToList();
        var completedMissions = seed.Missions
            .Where(m => string.Equals(m.Status, "Completed", StringComparison.Ordinal))
            .OrderBy(m => m.Id)
            .ToList();

        for (var index = 0; index < reusableMissionUnits.Count && completedMissions.Count > 0; index++)
        {
            var reusableItem = reusableMissionUnits[index];
            var mission = completedMissions[index % completedMissions.Count];
            var performedBy = ManagerForDepot(seed, reusableItem.DepotId!.Value);
            var exportedAt = (mission.StartTime ?? mission.CreatedAt ?? seed.StartUtc).AddMinutes(35 + index);
            var returnedAt = (mission.CompletedAt ?? exportedAt.AddHours(5)).AddMinutes(-20 + index % 6);

            inventoryLogs.Add(new InventoryLog
            {
                ReusableItemId = reusableItem.Id,
                ActionType = InventoryActionType.Export.ToString(),
                QuantityChange = 1,
                SourceType = InventorySourceType.Mission.ToString(),
                SourceId = mission.Id,
                MissionId = mission.Id,
                PerformedBy = performedBy,
                Note = $"Xuất {reusableItem.ItemModel?.Name ?? $"thiết bị #{reusableItem.ItemModelId}"} cho mission #{mission.Id}",
                CreatedAt = exportedAt
            });

            inventoryLogs.Add(new InventoryLog
            {
                ReusableItemId = reusableItem.Id,
                ActionType = InventoryActionType.Return.ToString(),
                QuantityChange = 1,
                SourceType = InventorySourceType.Mission.ToString(),
                SourceId = mission.Id,
                MissionId = mission.Id,
                PerformedBy = performedBy,
                Note = $"Nhận lại {reusableItem.ItemModel?.Name ?? $"thiết bị #{reusableItem.ItemModelId}"} sau mission #{mission.Id}",
                CreatedAt = returnedAt
            });
        }
    }

    private static Guid ManagerForDepot(DemoSeedContext seed, int depotId)
    {
        return seed.Managers[(depotId - 1) % seed.Managers.Count].Id;
    }

    private static Depot OperationalDepotForActivity(DemoSeedContext seed, int missionId, int step)
    {
        var operationalDepots = seed.Depots
            .Where(depot => !IsDepotClosureTestCandidate(depot))
            .ToList();

        return operationalDepots[(missionId + step) % operationalDepots.Count];
    }

    private static bool IsDepotClosureTestCandidate(Depot depot) =>
        DepotClosureTestDepotNames.Contains(depot.Name, StringComparer.Ordinal);

    private static IReadOnlyList<Depot> FindClosureTestDepots(DemoSeedContext seed) =>
        seed.Depots
            .Where(IsDepotClosureTestCandidate)
            .OrderBy(depot => depot.Id)
            .ToList();

    private static Depot? FindHueDepot(DemoSeedContext seed) =>
        seed.Depots.FirstOrDefault();

    private static HashSet<int> HueDepotExcludedItemModelIds(DemoSeedContext seed) =>
        seed.ItemModels
            .Where(item => item.Name != null
                && HueDepotExcludedItemNames.Contains(item.Name, StringComparer.OrdinalIgnoreCase))
            .Select(item => item.Id)
            .ToHashSet();

    private static void ExcludeHueDepotItems(DemoSeedContext seed)
    {
        var hueDepot = FindHueDepot(seed);
        if (hueDepot is null)
        {
            return;
        }

        var excludedItemModelIds = HueDepotExcludedItemModelIds(seed);
        if (excludedItemModelIds.Count == 0)
        {
            return;
        }

        seed.Inventories.RemoveAll(inventory =>
            inventory.DepotId == hueDepot.Id
            && inventory.ItemModelId.HasValue
            && excludedItemModelIds.Contains(inventory.ItemModelId.Value));
    }

    private static void ExcludeHueDepotReusableUnits(DemoSeedContext seed)
    {
        var hueDepot = FindHueDepot(seed);
        if (hueDepot is null)
        {
            return;
        }

        var excludedReusableModelIds = seed.ItemModels
            .Where(item => string.Equals(item.ItemType, nameof(ItemType.Reusable), StringComparison.OrdinalIgnoreCase))
            .Where(item => item.Name != null
                && HueDepotExcludedItemNames.Contains(item.Name, StringComparer.OrdinalIgnoreCase))
            .Select(item => item.Id)
            .ToHashSet();
        if (excludedReusableModelIds.Count == 0)
        {
            return;
        }

        seed.ReusableItems.RemoveAll(item =>
            item.DepotId == hueDepot.Id
            && item.ItemModelId.HasValue
            && excludedReusableModelIds.Contains(item.ItemModelId.Value));
    }

    private static void EnsureEssentialDepotStock(DemoSeedContext seed, ItemModel lifeJacketModel, ItemModel blanketModel)
    {
        for (var depotIndex = 0; depotIndex < seed.Depots.Count; depotIndex++)
        {
            var depot = seed.Depots[depotIndex];
            EnsureDepotInventory(seed, depot.Id, lifeJacketModel.Id, EssentialLifeJacketQuantity(depotIndex), depotIndex);
            EnsureDepotInventory(seed, depot.Id, blanketModel.Id, EssentialBlanketQuantity(depotIndex), depotIndex);
        }
    }

    private static void EnsureDepotInventory(DemoSeedContext seed, int depotId, int itemModelId, int quantity, int depotIndex)
    {
        var inventory = seed.Inventories.FirstOrDefault(i => i.DepotId == depotId && i.ItemModelId == itemModelId);
        if (inventory is null)
        {
            seed.Inventories.Add(new SupplyInventory
            {
                DepotId = depotId,
                ItemModelId = itemModelId,
                Quantity = quantity,
                MissionReservedQuantity = Math.Min(quantity / 10, 8),
                TransferReservedQuantity = Math.Min(quantity / 12, 6),
                LastStockedAt = seed.AnchorUtc.AddDays(-12 - depotIndex),
                IsDeleted = false
            });
            return;
        }

        inventory.Quantity = quantity;
        inventory.MissionReservedQuantity = Math.Min(quantity / 10, 8);
        inventory.TransferReservedQuantity = Math.Min(quantity / 12, 6);
        inventory.LastStockedAt = seed.AnchorUtc.AddDays(-12 - depotIndex);
        inventory.IsDeleted = false;
    }

    private static void EnsureClosureTestDepotsFullInventory(DemoSeedContext seed)
    {
        var closureDepots = FindClosureTestDepots(seed);
        if (closureDepots.Count == 0)
        {
            return;
        }

        foreach (var closureDepot in closureDepots)
        {
            foreach (var item in seed.ItemModels.OrderBy(model => model.Id))
            {
                var inventory = seed.Inventories.FirstOrDefault(i => i.DepotId == closureDepot.Id && i.ItemModelId == item.Id);
                if (inventory is null)
                {
                    var quantity = ClosureTestDepotQuantity(item);
                    seed.Inventories.Add(new SupplyInventory
                    {
                        DepotId = closureDepot.Id,
                        ItemModelId = item.Id,
                        Quantity = quantity,
                        MissionReservedQuantity = 0,
                        TransferReservedQuantity = 0,
                        LastStockedAt = seed.AnchorUtc.AddDays(-(18 + item.Id % 40)),
                        IsDeleted = false
                    });
                    continue;
                }

                inventory.MissionReservedQuantity = 0;
                inventory.TransferReservedQuantity = 0;
                inventory.LastStockedAt = seed.AnchorUtc.AddDays(-(18 + item.Id % 40));
                inventory.IsDeleted = false;
            }
        }
    }

    private static void EnsureEssentialBlanketLots(DemoSeedContext seed, ItemModel blanketModel)
    {
        var lotInventoryIds = seed.Lots
            .Select(l => l.SupplyInventoryId)
            .ToHashSet();
        var blanketInventories = seed.Inventories
            .Where(i => i.ItemModelId == blanketModel.Id)
            .OrderBy(i => i.DepotId)
            .ToList();

        foreach (var inventory in blanketInventories)
        {
            if (lotInventoryIds.Contains(inventory.Id))
            {
                continue;
            }

            var received = seed.AnchorUtc.AddDays(-45 - (inventory.DepotId ?? 0));
            seed.Lots.Add(new SupplyInventoryLot
            {
                SupplyInventoryId = inventory.Id,
                Quantity = inventory.Quantity ?? 0,
                RemainingQuantity = Math.Max(0, (inventory.Quantity ?? 0) - inventory.MissionReservedQuantity - inventory.TransferReservedQuantity),
                ReceivedDate = received,
                ExpiredDate = received.AddMonths(18),
                SourceType = InventorySourceType.Donation.ToString(),
                SourceId = 4_000 + inventory.Id,
                CreatedAt = received
            });
        }
    }

    private static void EnsureHueDepotExpiringConsumableLots(DemoSeedContext seed)
    {
        if (seed.Depots.Count == 0)
        {
            return;
        }

        var hueDepot = seed.Depots[0];
        var specs = new (string ItemName, int Quantity, int ReceivedOffsetDays, int ExpiredOffsetDays, int SourceId)[]
        {
            ("Mì tôm", 24, -20, 7, 90_001),
            ("Nước tinh khiết", 48, -18, 14, 90_002),
            ("Sữa bột trẻ em", 18, -16, 21, 90_003),
            ("Thuốc hạ sốt Paracetamol 500mg", 60, -14, 28, 90_004)
        };

        foreach (var spec in specs)
        {
            var itemModel = seed.ItemModels.Single(model =>
                string.Equals(model.Name, spec.ItemName, StringComparison.OrdinalIgnoreCase));
            var inventory = seed.Inventories.Single(inventory =>
                inventory.DepotId == hueDepot.Id && inventory.ItemModelId == itemModel.Id);
            var receivedDate = seed.AnchorUtc.AddDays(spec.ReceivedOffsetDays);

            seed.Lots.Add(new SupplyInventoryLot
            {
                SupplyInventoryId = inventory.Id,
                Quantity = spec.Quantity,
                RemainingQuantity = spec.Quantity,
                ReceivedDate = receivedDate,
                ExpiredDate = seed.AnchorUtc.AddDays(spec.ExpiredOffsetDays),
                SourceType = InventorySourceType.Purchase.ToString(),
                SourceId = spec.SourceId,
                CreatedAt = receivedDate
            });

            inventory.Quantity = (inventory.Quantity ?? 0) + spec.Quantity;
            inventory.LastStockedAt = receivedDate;
        }
    }

    private static void EnsureClosureTestDepotsConsumableLots(DemoSeedContext seed)
    {
        var closureDepots = FindClosureTestDepots(seed);
        if (closureDepots.Count == 0)
        {
            return;
        }

        var lotInventoryIds = seed.Lots
            .Select(lot => lot.SupplyInventoryId)
            .ToHashSet();

        foreach (var closureDepot in closureDepots)
        {
            foreach (var inventory in seed.Inventories
                         .Where(i => i.DepotId == closureDepot.Id && i.ItemModelId.HasValue)
                         .OrderBy(i => i.ItemModelId))
            {
                if (lotInventoryIds.Contains(inventory.Id))
                {
                    continue;
                }

                var itemModel = seed.ItemModels.Single(model => model.Id == inventory.ItemModelId!.Value);
                if (!string.Equals(itemModel.ItemType, nameof(ItemType.Consumable), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var receivedDate = seed.AnchorUtc.AddDays(-(45 + itemModel.Id % 60));
                seed.Lots.Add(new SupplyInventoryLot
                {
                    SupplyInventoryId = inventory.Id,
                    Quantity = inventory.Quantity ?? 0,
                    RemainingQuantity = inventory.Quantity ?? 0,
                    ReceivedDate = receivedDate,
                    ExpiredDate = receivedDate.AddMonths(8 + itemModel.Id % 10),
                    SourceType = InventorySourceType.Donation.ToString(),
                    SourceId = 120_000 + itemModel.Id,
                    CreatedAt = receivedDate
                });
            }
        }
    }

    private static void EnsureLifeJacketReusableUnits(DemoSeedContext seed, ItemModel lifeJacketModel)
    {
        var existingSerials = seed.ReusableItems
            .Where(item => item.SerialNumber != null)
            .Select(item => item.SerialNumber!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var depotIndex = 0; depotIndex < seed.Depots.Count; depotIndex++)
        {
            var depot = seed.Depots[depotIndex];
            var targetQuantity = seed.Inventories
                .Where(i => i.DepotId == depot.Id && i.ItemModelId == lifeJacketModel.Id)
                .Select(i => i.Quantity ?? 0)
                .Single();
            var existingCount = seed.ReusableItems.Count(item =>
                item.DepotId == depot.Id && item.ItemModelId == lifeJacketModel.Id);

            for (var unitIndex = existingCount; unitIndex < targetQuantity; unitIndex++)
            {
                var serialNumber = $"LIFEJACKET-D{depot.Id:00}-{unitIndex + 1:000}";
                if (!existingSerials.Add(serialNumber))
                {
                    continue;
                }

                seed.ReusableItems.Add(new ReusableItem
                {
                    DepotId = depot.Id,
                    ItemModelId = lifeJacketModel.Id,
                    SerialNumber = serialNumber,
                    Status = unitIndex % 19 == 0 ? "Maintenance" : unitIndex % 11 == 0 ? "Reserved" : "Available",
                    Condition = unitIndex % 23 == 0 ? "Fair" : "Good",
                    Note = unitIndex % 19 == 0 ? "Kiểm tra định kỳ trước mùa mưa bão" : null,
                    CreatedAt = seed.AnchorUtc.AddDays(-90 + (depotIndex * 7 + unitIndex) % 60),
                    UpdatedAt = seed.AnchorUtc.AddDays(-((depotIndex + unitIndex) % 25)),
                    IsDeleted = false
                });
            }
        }
    }

    private static void EnsureClosureTestDepotsReusableUnits(DemoSeedContext seed)
    {
        var closureDepots = FindClosureTestDepots(seed);
        if (closureDepots.Count == 0)
        {
            return;
        }

        var existingSerials = seed.ReusableItems
            .Where(item => item.SerialNumber != null)
            .Select(item => item.SerialNumber!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var closureDepot in closureDepots)
        {
            foreach (var inventory in seed.Inventories
                         .Where(i => i.DepotId == closureDepot.Id && i.ItemModelId.HasValue)
                         .OrderBy(i => i.ItemModelId))
            {
                var itemModel = seed.ItemModels.Single(model => model.Id == inventory.ItemModelId!.Value);
                if (!string.Equals(itemModel.ItemType, nameof(ItemType.Reusable), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var existingItem in seed.ReusableItems.Where(item =>
                             item.DepotId == closureDepot.Id
                             && item.ItemModelId == itemModel.Id))
                {
                    existingItem.Status = ReusableItemStatus.Available.ToString();
                    existingItem.Note = ClosureTestDepotReusableNote(closureDepot);
                    existingItem.UpdatedAt = seed.AnchorUtc.AddDays(-((itemModel.Id + existingItem.Id) % 18));
                }

                var targetQuantity = inventory.Quantity ?? 0;
                var existingCount = seed.ReusableItems.Count(item =>
                    item.DepotId == closureDepot.Id && item.ItemModelId == itemModel.Id);

                for (var unitIndex = existingCount; unitIndex < targetQuantity; unitIndex++)
                {
                    var serialNumber = $"PHY-DEPOT-D{closureDepot.Id:00}-M{itemModel.Id:000}-{unitIndex + 1:000}";
                    if (!existingSerials.Add(serialNumber))
                    {
                        continue;
                    }

                    seed.ReusableItems.Add(new ReusableItem
                    {
                        DepotId = closureDepot.Id,
                        ItemModelId = itemModel.Id,
                        SerialNumber = serialNumber,
                        Status = ReusableItemStatus.Available.ToString(),
                        Condition = "Good",
                        Note = ClosureTestDepotReusableNote(closureDepot),
                        CreatedAt = seed.AnchorUtc.AddDays(-(60 + (itemModel.Id + unitIndex) % 45)),
                        UpdatedAt = seed.AnchorUtc.AddDays(-((itemModel.Id + unitIndex) % 18)),
                        IsDeleted = false
                    });
                }
            }
        }
    }

    private static int ClosureTestDepotQuantity(ItemModel itemModel) =>
        string.Equals(itemModel.ItemType, nameof(ItemType.Reusable), StringComparison.OrdinalIgnoreCase)
            ? 4 + itemModel.Id % 3
            : 120 + (itemModel.Id % 5) * 20;

    private static string ClosureTestDepotReusableNote(Depot depot) =>
        $"Kho test đóng kho {depot.Name} - vật tư sẵn sàng chuyển kho.";

    private static void EnsureManagerReturnFixtureReusableUnits(DemoSeedContext seed)
    {
        if (seed.Depots.Count == 0)
        {
            return;
        }

        var hueDepot = seed.Depots[0];
        var reusableModelIdsWithEnoughUnits = seed.ReusableItems
            .Where(item => item.DepotId == hueDepot.Id
                && string.Equals(item.Status, nameof(ReusableItemStatus.Available), StringComparison.Ordinal)
                && item.ItemModelId.HasValue
                && seed.ItemModels.Any(model =>
                    model.Id == item.ItemModelId.Value
                    && string.Equals(model.ItemType, nameof(ItemType.Reusable), StringComparison.OrdinalIgnoreCase)))
            .GroupBy(item => item.ItemModelId!.Value)
            .Where(group => group.Count() >= 2)
            .Select(group => group.Key)
            .ToHashSet();

        if (reusableModelIdsWithEnoughUnits.Count >= 2)
        {
            return;
        }

        var existingSerials = seed.ReusableItems
            .Where(item => item.SerialNumber != null)
            .Select(item => item.SerialNumber!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidateModels = seed.ItemModels
            .Where(model => string.Equals(model.ItemType, nameof(ItemType.Reusable), StringComparison.OrdinalIgnoreCase))
            .OrderBy(model => model.Id)
            .ToList();

        foreach (var model in candidateModels)
        {
            if (reusableModelIdsWithEnoughUnits.Count >= 2)
            {
                break;
            }

            if (reusableModelIdsWithEnoughUnits.Contains(model.Id))
            {
                continue;
            }

            var availableCount = seed.ReusableItems.Count(item =>
                item.DepotId == hueDepot.Id
                && item.ItemModelId == model.Id
                && string.Equals(item.Status, nameof(ReusableItemStatus.Available), StringComparison.Ordinal));

            for (var unitIndex = availableCount; unitIndex < 2; unitIndex++)
            {
                var serialNumber = $"RETURN-FIXTURE-D{hueDepot.Id:00}-M{model.Id:000}-{unitIndex + 1:000}";
                if (!existingSerials.Add(serialNumber))
                {
                    continue;
                }

                seed.ReusableItems.Add(new ReusableItem
                {
                    DepotId = hueDepot.Id,
                    ItemModelId = model.Id,
                    SerialNumber = serialNumber,
                    Status = ReusableItemStatus.Available.ToString(),
                    Condition = "Good",
                    Note = "Demo manager01 return fixture reusable unit.",
                    CreatedAt = seed.AnchorUtc.AddDays(-45 - unitIndex),
                    UpdatedAt = seed.AnchorUtc.AddDays(-7 - unitIndex),
                    IsDeleted = false
                });
            }

            reusableModelIdsWithEnoughUnits.Add(model.Id);
        }
    }

    private static int EssentialLifeJacketQuantity(int depotIndex) =>
        50 + (35 + depotIndex * 13) % 51;

    private static int EssentialBlanketQuantity(int depotIndex) =>
        50 + (42 + depotIndex * 17) % 51;

    private static int? ResolveVatInvoiceId(IReadOnlyList<int> vatInvoiceIds, string sourceType, int? sourceId)
    {
        if (!string.Equals(sourceType, InventorySourceType.Purchase.ToString(), StringComparison.Ordinal) || vatInvoiceIds.Count == 0)
        {
            return null;
        }

        if (sourceId.HasValue && vatInvoiceIds.Contains(sourceId.Value))
        {
            return sourceId.Value;
        }

        return vatInvoiceIds[Math.Abs((sourceId ?? 1) - 1) % vatInvoiceIds.Count];
    }

    private static User CreateUser(string username, int roleId, int number, string lastName, string firstName, string password, SeedArea area, DemoSeedContext seed)
    {
        var rolePrefix = roleId switch
        {
            1 => "admin",
            2 => "coord",
            3 => "rescuer",
            4 => "manager",
            _ => "victim"
        };
        var location = Point(area.Lon + (number % 7 - 3) * 0.002, area.Lat + (number % 5 - 2) * 0.002);
        return new User
        {
            Id = StableGuid($"user-{rolePrefix}-{number:000}"),
            RoleId = roleId,
            FirstName = firstName,
            LastName = lastName,
            Username = username,
            Phone = Phone(roleId, number),
            Password = password,
            Email = $"{username}@resq.vn",
            IsEmailVerified = number % 17 != 0,
            AvatarUrl = $"https://i.pravatar.cc/160?u={username}",
            Location = location,
            Address = $"{10 + number % 90} {area.Address}",
            Ward = area.Ward,
            Province = area.Province,
            CreatedAt = seed.StartUtc.AddDays(number * 3 % 900),
            UpdatedAt = seed.AnchorUtc.AddDays(-(number % 60)),
            IsBanned = false
        };
    }

    private static User CreateDemoVictimWithPin(DemoSeedContext seed)
    {
        var area = Area(0);
        var user = CreateUser(
            "victim.demo.374745872",
            5,
            999,
            "Lê",
            "Minh Anh",
            SeedConstants.DemoVictimPinPasswordHash,
            area,
            seed);

        user.Phone = "+84374745872";
        user.Email = "victim.demo.374745872@resq.vn";
        user.Address = "32 Nguyễn Huệ, phường Phú Hội, Huế";
        user.Ward = "Phú Hội";
        user.Province = "Thừa Thiên Huế";
        user.Location = Point(107.5948, 16.4642);
        user.CreatedAt = new DateTime(2026, 4, 18, 10, 45, 0, DateTimeKind.Utc);
        user.UpdatedAt = new DateTime(2026, 4, 18, 10, 53, 8, DateTimeKind.Utc);
        user.IsEmailVerified = true;

        return user;
    }

    private static IEnumerable<UserRelativeProfile> CreateDemoVictimRelativeProfiles(Guid userId, DemoSeedContext seed)
    {
        var createdAt = new DateTime(2026, 4, 18, 10, 53, 8, DateTimeKind.Utc);
        var relatives = new[]
        {
            new RelativeProfileSeed(
                "Châu",
                "+84972513978",
                "ELDERLY",
                "FEMALE",
                ["me_gia", "can_diu", "uu_tien_so_tan"],
                "Mẹ 72 tuổi, huyết áp cao, hay đau khớp gối.",
                "Cần người dìu khi đi bộ xa hoặc leo cầu thang.",
                "Ăn mềm, hạn chế muối và đường.",
                Json(new
                {
                    bloodType = "UNKNOWN",
                    allergyDetails = "Dị ứng nhẹ với một số thuốc giảm đau nhóm NSAID.",
                    allergyOptions = new[] { "MEDICATION" },
                    medicalDevices = new[] { "WALKING_CANE" },
                    medicalHistory = new[] { "BONE_FRACTURE", "JOINT_PAIN" },
                    mobilityStatus = "NEEDS_ASSISTANCE",
                    specialSituation = new
                    {
                        isSenior = true,
                        isPregnant = false,
                        isYoungChild = false,
                        hasDisability = false
                    },
                    chronicConditions = new[] { "HYPERTENSION", "DIABETES" },
                    otherMedicalDevice = "",
                    longTermMedications = new[] { "Thuốc huyết áp buổi sáng", "Thuốc tiểu đường sau ăn" },
                    hasLongTermMedication = true,
                    medicalHistoryDetails = "Từng gãy xương cổ tay phải, đi lại chậm khi trời mưa.",
                    otherChronicCondition = ""
                })),
            new RelativeProfileSeed(
                "An",
                "+84908112233",
                "ADULT",
                "FEMALE",
                ["vo", "lien_he_chinh", "di_chuyen_duoc"],
                "Sức khỏe ổn định, có tiền sử hen nhẹ khi lạnh.",
                "Cần mang theo thuốc xịt hen dự phòng.",
                "Không ăn hải sản sống.",
                Json(new
                {
                    bloodType = "O",
                    allergyDetails = "Dị ứng hải sản sống.",
                    allergyOptions = new[] { "FOOD" },
                    medicalDevices = Array.Empty<string>(),
                    medicalHistory = new[] { "ASTHMA" },
                    mobilityStatus = "NORMAL",
                    specialSituation = new
                    {
                        isSenior = false,
                        isPregnant = false,
                        isYoungChild = false,
                        hasDisability = false
                    },
                    chronicConditions = Array.Empty<string>(),
                    otherMedicalDevice = "",
                    longTermMedications = new[] { "Thuốc xịt hen dự phòng" },
                    hasLongTermMedication = true,
                    medicalHistoryDetails = "Hen nhẹ, thường xuất hiện khi thời tiết lạnh hoặc ẩm.",
                    otherChronicCondition = ""
                })),
            new RelativeProfileSeed(
                "Thảo",
                "+84933668120",
                "ADULT",
                "FEMALE",
                ["chi_gai", "biet_so_cuu", "co_the_ho_tro"],
                "Chị gái sống gần nhà, có thể hỗ trợ chăm sóc người già.",
                null,
                "Không ăn cay.",
                Json(new
                {
                    bloodType = "B",
                    allergyDetails = "",
                    allergyOptions = Array.Empty<string>(),
                    medicalDevices = Array.Empty<string>(),
                    medicalHistory = Array.Empty<string>(),
                    mobilityStatus = "NORMAL",
                    specialSituation = new
                    {
                        isSenior = false,
                        isPregnant = false,
                        isYoungChild = false,
                        hasDisability = false
                    },
                    chronicConditions = Array.Empty<string>(),
                    otherMedicalDevice = "",
                    longTermMedications = Array.Empty<string>(),
                    hasLongTermMedication = false,
                    medicalHistoryDetails = "",
                    otherChronicCondition = ""
                })),
            new RelativeProfileSeed(
                "Khoa",
                "+84911224567",
                "ADULT",
                "MALE",
                ["em_trai", "can_lien_lac", "di_chuyen_duoc"],
                "Em trai thường đi làm xa, cần báo sớm khi có sơ tán.",
                "Cần hỗ trợ định vị nếu mất sóng điện thoại.",
                null,
                Json(new
                {
                    bloodType = "A",
                    allergyDetails = "",
                    allergyOptions = new[] { "DUST" },
                    medicalDevices = Array.Empty<string>(),
                    medicalHistory = new[] { "MIGRAINE" },
                    mobilityStatus = "NORMAL",
                    specialSituation = new
                    {
                        isSenior = false,
                        isPregnant = false,
                        isYoungChild = false,
                        hasDisability = false
                    },
                    chronicConditions = Array.Empty<string>(),
                    otherMedicalDevice = "",
                    longTermMedications = Array.Empty<string>(),
                    hasLongTermMedication = false,
                    medicalHistoryDetails = "Đôi khi đau nửa đầu khi thiếu ngủ.",
                    otherChronicCondition = ""
                }))
        };

        return relatives.Select((relative, index) => new UserRelativeProfile
        {
            Id = StableGuid($"demo-victim-relative-{index + 1}"),
            UserId = userId,
            DisplayName = relative.DisplayName,
            PhoneNumber = relative.PhoneNumber,
            PersonType = relative.PersonType,
            RelationGroup = "gia_dinh",
            Gender = relative.Gender,
            TagsJson = Json(relative.Tags),
            MedicalBaselineNote = relative.MedicalBaselineNote,
            SpecialNeedsNote = relative.SpecialNeedsNote,
            SpecialDietNote = relative.SpecialDietNote,
            MedicalProfileJson = relative.MedicalProfileJson,
            ProfileUpdatedAt = createdAt.AddMinutes(index),
            CreatedAt = createdAt.AddSeconds(index * 12),
            UpdatedAt = createdAt.AddSeconds(index * 12 + 4)
        });
    }

    private static bool IsRecentRescuerNumber(int number) =>
        number > TotalRescuerCount - RecentRescuerCount;

    private static int RecentRescuerIndex(int number) =>
        number - (TotalRescuerCount - RecentRescuerCount) - 1;

    private static DateTime RecentRescuerCreatedAt(DemoSeedContext seed, int recentIndex)
    {
        var anchorVietnamDate = seed.AnchorUtc.AddHours(7).Date;
        var dayOffset = -29 + recentIndex * 27 / Math.Max(1, RecentRescuerCount - 1);
        var localCreatedAt = anchorVietnamDate
            .AddDays(dayOffset)
            .AddHours(8 + recentIndex % 10)
            .AddMinutes(recentIndex * 17 % 60);

        return VnToUtc(localCreatedAt);
    }

    private static DateTime RecentRescuerApprovedAt(DemoSeedContext seed, DateTime? createdAt, int recentIndex)
    {
        var approvedAt = (createdAt ?? RecentRescuerCreatedAt(seed, recentIndex))
            .AddDays(1 + recentIndex % 3)
            .AddHours(2);

        return approvedAt <= seed.AnchorUtc
            ? approvedAt
            : seed.AnchorUtc.AddHours(-(recentIndex % 12 + 1));
    }

    private static string Phone(int roleId, int number)
    {
        var prefix = roleId switch
        {
            1 => 900,
            2 => 901,
            3 => 902,
            4 => 903,
            5 => 904,
            _ => 905
        };
        return roleId == 5
            ? $"+84{prefix}{number:000000}"
            : $"0{prefix}{number:000000}";
    }

    private static Guid StableGuid(string value)
    {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(value));
        return new Guid(hash);
    }

    private static DateTime VnToUtc(DateTime vietnamLocal)
    {
        return DateTime.SpecifyKind(vietnamLocal - TimeSpan.FromHours(7), DateTimeKind.Utc);
    }

    private static string Json(object value) => JsonSerializer.Serialize(value, JsonOptions);

    private static Point Point(double longitude, double latitude)
    {
        return new Point(longitude, latitude) { SRID = 4326 };
    }

    private static Point? OffsetPoint(Point? point, double latOffset, double lonOffset)
    {
        if (point is null)
        {
            return null;
        }

        return Point(point.X + lonOffset, point.Y + latOffset);
    }

    private static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double radius = 6371;
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2))
            * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return radius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;

    private static DateTime RandomEventLocal(DemoSeedContext seed, int index)
    {
        var yearBucket = index % 10;
        var year = yearBucket < 2 ? 2023 : yearBucket < 5 ? 2024 : yearBucket < 8 ? 2025 : 2026;
        var seasonBucket = index % 20;
        int month;
        if (seasonBucket < 13 && year < 2026)
        {
            month = 9 + index % 4;
        }
        else if (seasonBucket < 17)
        {
            month = 1 + index % 3;
        }
        else
        {
            month = year == 2023 ? 4 + index % 5 : 4 + index % 5;
            if (year == 2026)
            {
                month = 4;
            }
        }

        if (year == 2023 && month < 4)
        {
            month = 4;
        }
        if (year == 2026 && month > 4)
        {
            month = 4;
        }

        var maxDay = year == 2026 && month == 4 ? 16 : DateTime.DaysInMonth(year, month);
        var minDay = year == 2023 && month == 4 ? 16 : 1;
        var day = minDay + index % (maxDay - minDay + 1);
        return new DateTime(year, month, day, index % 24, (index * 7) % 60, 0, DateTimeKind.Unspecified);
    }

    private static DateTime RandomEventUtc(DemoSeedContext seed, int index) => VnToUtc(RandomEventLocal(seed, index));

    private static bool IsRecentOpenSosStatus(string? status) => status is "Pending" or "Assigned" or "InProgress" or "Incident";

    private static (DateTime CreatedAt, DateTime ReceivedAt, DateTime? ReviewedAt, DateTime LastUpdatedAt) BuildRecentOpenSosTimeline(
        DateTime anchorUtc,
        int primaryIndex,
        int secondaryIndex,
        string status,
        bool onBehalf)
    {
        if (!IsRecentOpenSosStatus(status))
        {
            throw new InvalidOperationException($"Status '{status}' does not use recent open SOS timeline.");
        }

        var seed = StableGuid($"recent-open-sos-{status}-{primaryIndex}-{secondaryIndex}-{(onBehalf ? 1 : 0)}");
        var createdHoursAgo = status switch
        {
            "Pending" => DeterministicRange(seed, 0, 2.5, 23.5),
            "Assigned" => DeterministicRange(seed, 0, 4.0, 21.0),
            "InProgress" => DeterministicRange(seed, 0, 5.5, 18.0),
            _ => DeterministicRange(seed, 0, 6.0, 14.0)
        };

        var createdAt = TrimUtcToMinute(anchorUtc.AddHours(-createdHoursAgo));
        var receivedAt = TrimUtcToMinute(createdAt.AddMinutes(onBehalf ? 2 : 0));

        if (status == "Pending")
        {
            var pendingLastUpdatedAt = TrimUtcToMinute(createdAt.AddMinutes(DeterministicRange(seed, 4, 12, 55)));
            return (createdAt, receivedAt, null, MinUtc(pendingLastUpdatedAt, anchorUtc.AddMinutes(-1)));
        }

        var reviewedAt = TrimUtcToMinute(createdAt.AddMinutes(DeterministicRange(seed, 4, 8, 45)));
        var followUpMinutes = status switch
        {
            "Assigned" => DeterministicRange(seed, 8, 18, 95),
            "InProgress" => DeterministicRange(seed, 8, 55, 220),
            _ => DeterministicRange(seed, 8, 70, 260)
        };
        var lastUpdatedAtCandidate = TrimUtcToMinute(reviewedAt.AddMinutes(followUpMinutes));
        var lastUpdatedAt = MinUtc(lastUpdatedAtCandidate, anchorUtc.AddMinutes(-1));
        if (lastUpdatedAt < reviewedAt)
        {
            lastUpdatedAt = reviewedAt;
        }

        return (createdAt, receivedAt, reviewedAt, lastUpdatedAt);
    }

    private static (DateTime CreatedAt, DateTime ReceivedAt, DateTime? ReviewedAt, DateTime LastUpdatedAt) BuildClampedHistoricalSosTimeline(
        DateTime createdAtCandidate,
        TimeSpan receivedOffset,
        TimeSpan? reviewedOffset,
        TimeSpan lastUpdatedOffset,
        DateTime anchorUtc)
    {
        var createdAt = ClampHistoricalUtc(createdAtCandidate, anchorUtc);
        var receivedAt = ClampHistoricalUtc(createdAtCandidate.Add(receivedOffset), createdAt, anchorUtc);
        DateTime? reviewedAt = reviewedOffset.HasValue
            ? ClampHistoricalUtc(createdAtCandidate.Add(reviewedOffset.Value), receivedAt, anchorUtc)
            : null;
        var lastUpdatedLowerBound = reviewedAt ?? receivedAt;
        var lastUpdatedAt = ClampHistoricalUtc(createdAtCandidate.Add(lastUpdatedOffset), lastUpdatedLowerBound, anchorUtc);
        return (createdAt, receivedAt, reviewedAt, lastUpdatedAt);
    }

    private static DateTime TrimUtcToMinute(DateTime value) =>
        new DateTime(value.Ticks - value.Ticks % TimeSpan.TicksPerMinute, DateTimeKind.Utc);

    private static DateTime ClampHistoricalUtc(DateTime candidateUtc, DateTime anchorUtc) =>
        candidateUtc <= anchorUtc ? candidateUtc : anchorUtc;

    private static DateTime ClampHistoricalUtc(DateTime candidateUtc, DateTime floorUtc, DateTime anchorUtc)
    {
        var capped = ClampHistoricalUtc(candidateUtc, anchorUtc);
        return capped < floorUtc ? floorUtc : capped;
    }

    private static DateTime? ClampHistoricalUtc(DateTime? candidateUtc, DateTime floorUtc, DateTime anchorUtc) =>
        candidateUtc.HasValue ? ClampHistoricalUtc(candidateUtc.Value, floorUtc, anchorUtc) : null;

    private static DateTime MinUtc(DateTime left, DateTime right) => left <= right ? left : right;

    private static SeedArea Area(int index)
    {
        var areas = new[]
        {
            new SeedArea("HUE", "Thừa Thiên Huế", "Phú Hội", "Lê Lợi, Huế", 16.4637, 107.5962),
            new SeedArea("HUE", "Thừa Thiên Huế", "Hương Sơ", "Nguyễn Văn Linh, Huế", 16.4952, 107.5860),
            new SeedArea("DNG", "Đà Nẵng", "Hải Châu", "2 Tháng 9, Đà Nẵng", 16.0471, 108.2188),
            new SeedArea("QTR", "Quảng Trị", "Đông Hà", "Lê Duẩn, Đông Hà", 16.8175, 107.1003),
            new SeedArea("QNM", "Quảng Nam", "Tam Kỳ", "Hùng Vương, Tam Kỳ", 15.5736, 108.4740),
            new SeedArea("QNM", "Quảng Nam", "Hội An", "Cửa Đại, Hội An", 15.8801, 108.3380),
            new SeedArea("QNG", "Quảng Ngãi", "Trần Phú", "Quang Trung, Quảng Ngãi", 15.1214, 108.8044)
        };
        return areas[index % areas.Length];
    }

    private static IReadOnlyList<SeedCoordinate> GetCuratedBulkSosAnchors(SeedArea area)
    {
        return (area.Code, area.Ward) switch
        {
            ("HUE", "Phú Hội") =>
            [
                new SeedCoordinate(16.466942, 107.593184),
                new SeedCoordinate(16.465718, 107.599862),
                new SeedCoordinate(16.463981, 107.603104),
                new SeedCoordinate(16.461447, 107.598756),
                new SeedCoordinate(16.459832, 107.594227),
                new SeedCoordinate(16.458214, 107.601335),
                new SeedCoordinate(16.456973, 107.589684),
                new SeedCoordinate(16.468256, 107.588927)
            ],
            ("HUE", "Hương Sơ") =>
            [
                new SeedCoordinate(16.499812, 107.582644),
                new SeedCoordinate(16.498276, 107.589401),
                new SeedCoordinate(16.496145, 107.592338),
                new SeedCoordinate(16.493447, 107.588924),
                new SeedCoordinate(16.491318, 107.583571),
                new SeedCoordinate(16.489642, 107.590811),
                new SeedCoordinate(16.487925, 107.586374),
                new SeedCoordinate(16.500386, 107.586922)
            ],
            ("DNG", "Hải Châu") =>
            [
                new SeedCoordinate(16.050284, 108.214973),
                new SeedCoordinate(16.048617, 108.221556),
                new SeedCoordinate(16.046851, 108.225348),
                new SeedCoordinate(16.044902, 108.220217),
                new SeedCoordinate(16.042731, 108.216094),
                new SeedCoordinate(16.045588, 108.212347),
                new SeedCoordinate(16.051936, 108.219487),
                new SeedCoordinate(16.047934, 108.228204)
            ],
            ("QTR", "Đông Hà") =>
            [
                new SeedCoordinate(16.821476, 107.096412),
                new SeedCoordinate(16.820138, 107.103754),
                new SeedCoordinate(16.817462, 107.107118),
                new SeedCoordinate(16.814808, 107.102671),
                new SeedCoordinate(16.812943, 107.098384),
                new SeedCoordinate(16.815562, 107.094945),
                new SeedCoordinate(16.819947, 107.090853),
                new SeedCoordinate(16.823114, 107.100226)
            ],
            ("QNM", "Tam Kỳ") =>
            [
                new SeedCoordinate(15.577812, 108.469826),
                new SeedCoordinate(15.576203, 108.476915),
                new SeedCoordinate(15.573941, 108.480144),
                new SeedCoordinate(15.571225, 108.476492),
                new SeedCoordinate(15.569438, 108.472318),
                new SeedCoordinate(15.571987, 108.467834),
                new SeedCoordinate(15.575112, 108.463925),
                new SeedCoordinate(15.578431, 108.473781)
            ],
            ("QNM", "Hội An") =>
            [
                new SeedCoordinate(15.884216, 108.334882),
                new SeedCoordinate(15.882807, 108.341245),
                new SeedCoordinate(15.880352, 108.344627),
                new SeedCoordinate(15.877966, 108.340982),
                new SeedCoordinate(15.876184, 108.336771),
                new SeedCoordinate(15.878415, 108.332684),
                new SeedCoordinate(15.881924, 108.329415),
                new SeedCoordinate(15.885142, 108.338904)
            ],
            ("QNG", "Trần Phú") =>
            [
                new SeedCoordinate(15.125116, 108.800712),
                new SeedCoordinate(15.123728, 108.807194),
                new SeedCoordinate(15.121335, 108.810456),
                new SeedCoordinate(15.118914, 108.806973),
                new SeedCoordinate(15.117103, 108.802624),
                new SeedCoordinate(15.119684, 108.798935),
                new SeedCoordinate(15.122447, 108.795682),
                new SeedCoordinate(15.126024, 108.804173)
            ],
            _ => throw new InvalidOperationException($"Chưa cấu hình curated anchor cho area {area.Code}/{area.Ward}.")
        };
    }

    private static IReadOnlyList<SeedCoordinate> BuildClusterScatterPoints(SeedArea area, int clusterIndex, int count)
    {
        var anchors = GetCuratedBulkSosAnchors(area);
        var anchorSeed = StableGuid($"cluster-anchor-{clusterIndex}-{area.Code}-{area.Ward}");
        var anchorIndex = (DeterministicIndex(anchorSeed, anchors.Count) + clusterIndex % anchors.Count) % anchors.Count;
        var anchor = anchors[anchorIndex];

        var templates = count switch
        {
            1 => SinglePointClusterScatterTemplates,
            2 => TwoPointClusterScatterTemplates,
            3 => ThreePointClusterScatterTemplates,
            4 => FourPointClusterScatterTemplates,
            _ => throw new InvalidOperationException($"Không hỗ trợ {count} SOS trong một cluster demo.")
        };
        var templateSeed = StableGuid($"cluster-template-{clusterIndex}-{count}-{area.Code}-{area.Ward}");
        var templateIndex = (DeterministicIndex(templateSeed, templates.Length) + clusterIndex % templates.Length) % templates.Length;
        var template = templates[templateIndex];
        var latJitterAmplitude = count switch
        {
            1 => 0.00018,
            2 => 0.00024,
            _ => 0.00035
        };
        var lonJitterAmplitude = count switch
        {
            1 => 0.00022,
            2 => 0.00030,
            _ => 0.00042
        };

        return Enumerable.Range(0, count)
            .Select(pointIndex =>
            {
                var jitterSeed = StableGuid($"cluster-point-{clusterIndex}-{pointIndex}-{area.Code}-{area.Ward}");
                var latJitter = DeterministicRange(jitterSeed, 0, -latJitterAmplitude, latJitterAmplitude);
                var lonJitter = DeterministicRange(jitterSeed, 4, -lonJitterAmplitude, lonJitterAmplitude);
                return new SeedCoordinate(
                    anchor.Lat + template[pointIndex].Lat + latJitter,
                    anchor.Lon + template[pointIndex].Lon + lonJitter);
            })
            .ToArray();
    }

    private static int DeterministicIndex(Guid seed, int count)
    {
        var bytes = seed.ToByteArray();
        return (int)(BitConverter.ToUInt32(bytes, 0) % count);
    }

    private static double DeterministicRange(Guid seed, int byteOffset, double min, double max)
    {
        var bytes = seed.ToByteArray();
        var offset = Math.Clamp(byteOffset, 0, bytes.Length - sizeof(uint));
        var ratio = BitConverter.ToUInt32(bytes, offset) / (double)uint.MaxValue;
        return min + (max - min) * ratio;
    }

    private static readonly SeedCoordinate[][] SinglePointClusterScatterTemplates =
    [
        [new SeedCoordinate(0.00042, -0.00028)],
        [new SeedCoordinate(-0.00031, 0.00047)],
        [new SeedCoordinate(0.00018, 0.00039)],
        [new SeedCoordinate(-0.00046, -0.00012)]
    ];

    private static readonly SeedCoordinate[][] TwoPointClusterScatterTemplates =
    [
        [
            new SeedCoordinate(0.00074, -0.00041),
            new SeedCoordinate(-0.00028, 0.00063)
        ],
        [
            new SeedCoordinate(-0.00061, -0.00022),
            new SeedCoordinate(0.00047, 0.00058)
        ],
        [
            new SeedCoordinate(0.00032, -0.00079),
            new SeedCoordinate(-0.00054, 0.00035)
        ],
        [
            new SeedCoordinate(-0.00072, 0.00048),
            new SeedCoordinate(0.00026, -0.00057)
        ]
    ];

    private static readonly SeedCoordinate[][] ThreePointClusterScatterTemplates =
    [
        [
            new SeedCoordinate(0.0018, -0.0011),
            new SeedCoordinate(-0.0007, 0.0016),
            new SeedCoordinate(0.0003, -0.0022)
        ],
        [
            new SeedCoordinate(0.0012, 0.0017),
            new SeedCoordinate(-0.0014, -0.0006),
            new SeedCoordinate(0.0005, -0.0018)
        ],
        [
            new SeedCoordinate(0.0009, -0.0019),
            new SeedCoordinate(-0.0016, 0.0008),
            new SeedCoordinate(0.0017, 0.0011)
        ],
        [
            new SeedCoordinate(0.0015, 0.0006),
            new SeedCoordinate(-0.0009, -0.0017),
            new SeedCoordinate(-0.0018, 0.0014)
        ]
    ];

    private static readonly SeedCoordinate[][] FourPointClusterScatterTemplates =
    [
        [
            new SeedCoordinate(0.0019, -0.0012),
            new SeedCoordinate(-0.0006, 0.0017),
            new SeedCoordinate(0.0004, -0.0021),
            new SeedCoordinate(-0.0017, 0.0009)
        ],
        [
            new SeedCoordinate(0.0013, 0.0018),
            new SeedCoordinate(-0.0015, -0.0007),
            new SeedCoordinate(0.0006, -0.0019),
            new SeedCoordinate(-0.0004, 0.0022)
        ],
        [
            new SeedCoordinate(0.0021, 0.0005),
            new SeedCoordinate(-0.0008, -0.0018),
            new SeedCoordinate(-0.0019, 0.0012),
            new SeedCoordinate(0.0003, -0.0024)
        ],
        [
            new SeedCoordinate(0.0011, -0.0020),
            new SeedCoordinate(-0.0017, 0.0006),
            new SeedCoordinate(0.0018, 0.0014),
            new SeedCoordinate(-0.0005, 0.0020)
        ]
    ];

    private static (string Last, string First) VietnameseName(int index)
    {
        var lastNames = new[] { "Nguyễn", "Trần", "Lê", "Phạm", "Hoàng", "Huỳnh", "Phan", "Võ", "Đặng", "Bùi", "Đỗ", "Hồ", "Ngô", "Dương", "Lý" };
        var firstNames = new[]
        {
            "Anh Tuấn", "Khánh Vy", "Minh Châu", "Quang Hải", "Thảo Nguyên", "Hoài Nam", "Thanh Hằng", "Đức Anh", "Mai Lan", "Gia Huy",
            "Hồng Nhung", "Bảo Trâm", "Văn Đức", "Thanh Tâm", "Nhật Minh", "Phương Linh", "Mạnh Hùng", "Diệu Anh", "Quốc Bảo", "Ngọc Hà"
        };
        return (lastNames[index % lastNames.Length], firstNames[index % firstNames.Length]);
    }

    private static string FullName(User user) => $"{user.LastName} {user.FirstName}".Trim();

    private static string TeamName(int index)
    {
        var names = new[] { "Hương Giang", "Bạch Mã", "Sơn Trà", "Hải Vân", "Thạch Hãn", "Thu Bồn", "Trà Khúc", "Phú Bài" };
        return names[index % names.Length];
    }

    private static string TeamMemberRole(int index, string? teamType)
    {
        if (teamType == "Medical")
        {
            return index % 2 == 0 ? "Medic" : "Support";
        }

        if (teamType == "Transportation")
        {
            return index % 2 == 0 ? "Driver" : "Loader";
        }

        return index % 3 == 0 ? "Navigator" : "Rescuer";
    }

    private static string Slug(string value)
    {
        var normalized = value.ToLowerInvariant()
            .Replace(" ", "-", StringComparison.Ordinal)
            .Replace("đ", "d", StringComparison.Ordinal);
        var builder = new StringBuilder();
        foreach (var ch in normalized)
        {
            if (char.IsAsciiLetterOrDigit(ch) || ch == '-')
            {
                builder.Append(ch);
            }
        }
        return builder.ToString();
    }

    private static List<User> GetDeployableRescuers(DemoSeedContext seed) =>
        seed.Rescuers.Take(seed.Rescuers.Count - UnassignedRescuerCount).ToList();

    private static bool IsHueStadiumReserveTeam(RescueTeam team) =>
        team.Code?.StartsWith(HueStadiumReserveTeamCodePrefix, StringComparison.Ordinal) == true;

    private static AssemblyPoint? GetHueStadiumAssemblyPoint(DemoSeedContext seed) =>
        seed.AssemblyPoints.FirstOrDefault(point =>
            string.Equals(point.Code, "AP-HUE-TD-241015", StringComparison.Ordinal)
            || string.Equals(point.Name, "Sân vận động Tự Do (Thừa Thiên Huế)", StringComparison.Ordinal));

    private static IEnumerable<ServiceZone> ServiceZones(DateTime now)
        => ServiceZoneSeedData.CreateZones(now);

    private static IReadOnlyList<DocumentFileType> DocumentFileTypes(DateTime now) =>
    [
        new DocumentFileType
        {
            Id = 1,
            Code = "WATER_SAFETY_CERT",
            Name = "Chứng chỉ an toàn dưới nước",
            Description = "Chứng chỉ xác nhận khả năng bơi lội, sinh tồn và an toàn môi trường nước cơ bản.",
            IsActive = true,
            DocumentFileTypeCategoryId = 1,
            CreatedAt = now,
            UpdatedAt = now
        },
        new DocumentFileType
        {
            Id = 2,
            Code = "WATER_RESCUE_CERT",
            Name = "Chứng chỉ cứu hộ dưới nước",
            Description = "Chứng chỉ nghiệp vụ cứu hộ, cứu nạn chuyên nghiệp dưới nước, dòng chảy xiết.",
            IsActive = true,
            DocumentFileTypeCategoryId = 1,
            CreatedAt = now,
            UpdatedAt = now
        },
        new DocumentFileType
        {
            Id = 3,
            Code = "TECHNICAL_RESCUE_CERT",
            Name = "Chứng chỉ cứu hộ kỹ thuật",
            Description = "Chứng chỉ nghiệp vụ sử dụng thiết bị chuyên dụng, cứu hộ không gian hẹp, sập đổ, dùng dây thừng.",
            IsActive = true,
            DocumentFileTypeCategoryId = 1,
            CreatedAt = now,
            UpdatedAt = now
        },
        new DocumentFileType
        {
            Id = 4,
            Code = "DISASTER_RESPONSE_CERT",
            Name = "Chứng chỉ ứng phó thiên tai",
            Description = "Chứng chỉ hoàn thành khóa huấn luyện phản ứng nhanh, điều phối và ứng phó thảm họa/thiên tai.",
            IsActive = true,
            DocumentFileTypeCategoryId = 1,
            CreatedAt = now,
            UpdatedAt = now
        },
        new DocumentFileType
        {
            Id = 5,
            Code = "BASIC_FIRST_AID_CERT",
            Name = "Chứng chỉ Sơ cấp cứu cơ bản",
            Description = "Chứng chỉ hoàn thành các khóa đào tạo sơ cấp cứu ban đầu, hô hấp nhân tạo, dành cho tình nguyện viên và nhân viên y tế nền tảng.",
            IsActive = true,
            DocumentFileTypeCategoryId = 2,
            CreatedAt = now,
            UpdatedAt = now
        },
        new DocumentFileType
        {
            Id = 6,
            Code = "NURSING_PRACTICE_LICENSE",
            Name = "Chứng chỉ hành nghề Điều dưỡng",
            Description = "Giấy phép hành nghề điều dưỡng, y tá do cơ quan có thẩm quyền cấp, chứng minh năng lực thực hành lâm sàng và chăm sóc người bệnh.",
            IsActive = true,
            DocumentFileTypeCategoryId = 2,
            CreatedAt = now,
            UpdatedAt = now
        },
        new DocumentFileType
        {
            Id = 7,
            Code = "MOTORCYCLE_LICENSE",
            Name = "Giấy phép lái xe máy",
            Description = "Bằng lái xe mô tô 2 bánh (Hạng A1, A2...).",
            IsActive = true,
            DocumentFileTypeCategoryId = 3,
            CreatedAt = now,
            UpdatedAt = now
        },
        new DocumentFileType
        {
            Id = 8,
            Code = "CAR_TRUCK_LICENSE",
            Name = "Giấy phép lái xe ô tô / tải",
            Description = "Bằng lái xe ô tô, xe bán tải, xe tải hạng nặng (Hạng B1, B2, C, D...).",
            IsActive = true,
            DocumentFileTypeCategoryId = 3,
            CreatedAt = now,
            UpdatedAt = now
        },
        new DocumentFileType
        {
            Id = 9,
            Code = "OTHER",
            Name = "Khác",
            Description = "Khác",
            IsActive = true,
            DocumentFileTypeCategoryId = 4,
            CreatedAt = now,
            UpdatedAt = now
        },
        new DocumentFileType
        {
            Id = 10,
            Code = "PARAMEDIC_EMT_CERT",
            Name = "Chứng chỉ Cấp cứu ngoại viện",
            Description = "Chứng chỉ chuyên môn dành cho lực lượng cấp cứu tiền viện (115/EMT), chuyên gia xử lý chấn thương và duy trì sự sống trực tiếp tại hiện trường.",
            IsActive = true,
            DocumentFileTypeCategoryId = 2,
            CreatedAt = now,
            UpdatedAt = now
        },
        new DocumentFileType
        {
            Id = 11,
            Code = "MEDICAL_DOCTOR_LICENSE",
            Name = "Chứng chỉ hành nghề Bác sĩ",
            Description = "Giấy phép hành nghề khám, chữa bệnh cấp cho Bác sĩ. Thể hiện thẩm quyền cao nhất trong chẩn đoán, phân loại mức độ nguy kịch và ra y lệnh.",
            IsActive = true,
            DocumentFileTypeCategoryId = 2,
            CreatedAt = now,
            UpdatedAt = now
        },
        new DocumentFileType
        {
            Id = 12,
            Code = "INLAND_WATERWAY_LICENSE",
            Name = "Bằng lái phương tiện thủy",
            Description = "Chứng chỉ/Bằng lái phương tiện thủy nội địa dành cho người điều khiển Ca nô, xuồng máy có động cơ.",
            IsActive = true,
            DocumentFileTypeCategoryId = 3,
            CreatedAt = now,
            UpdatedAt = now
        }
    ];

    private static IReadOnlyList<ItemTemplate> BaseItemModels()
    {
        return
        [
            new("Food", "Mì tôm", "Mì ăn liền đóng gói dùng cứu trợ khẩn cấp", "gói", "Consumable", 0.8m, 0.075m),
            new("Food", "Sữa bột trẻ em", "Sữa bột dinh dưỡng dành cho trẻ em dưới 6 tuổi", "gói", "Consumable", 0.5m, 0.4m),
            new("Food", "Lương khô", "Lương khô năng lượng cao, bảo quản lâu dài", "thanh", "Consumable", 0.15m, 0.06m),
            new("Food", "Gạo sấy khô", "Gạo sấy khô ăn liền, chỉ cần thêm nước nóng", "gói", "Consumable", 0.6m, 0.5m),
            new("Food", "Cháo ăn liền", "Cháo ăn liền đóng gói, dễ tiêu hóa cho mọi lứa tuổi", "gói", "Consumable", 0.4m, 0.065m),
            new("Food", "Bánh mì khô", "Bánh mì khô bảo quản lâu, tiện lợi khi cứu trợ", "gói", "Consumable", 0.8m, 0.15m),
            new("Food", "Muối tinh", "Muối tinh tiêu chuẩn dùng chế biến thực phẩm", "gói", "Consumable", 0.2m, 0.25m),
            new("Food", "Đường cát trắng", "Đường cát trắng tinh luyện dùng pha chế và nấu ăn", "gói", "Consumable", 0.35m, 0.5m),
            new("Food", "Dầu ăn thực vật", "Dầu ăn thực vật đóng chai dùng chế biến thực phẩm", "chai", "Consumable", 1.2m, 1.0m),
            new("Food", "Thịt hộp đóng gói", "Thịt hộp đóng gói bảo quản lâu, giàu dinh dưỡng", "hộp", "Consumable", 0.5m, 0.35m),
            new("Water", "Nước tinh khiết", "Nước uống đóng chai 500ml phục vụ cấp phát", "chai", "Consumable", 0.6m, 0.52m),
            new("Water", "Nước lọc bình 20L", "Bình nước lọc 20 lít phục vụ sinh hoạt tập thể", "bình", "Consumable", 22.0m, 20.5m),
            new("Water", "Viên lọc nước khẩn cấp", "Viên lọc nước cầm tay, xử lý nước bẩn thành nước uống", "viên", "Consumable", 0.005m, 0.004m),
            new("Water", "Chai nước Aquafina", "Nước tinh khiết Aquafina đóng chai 500ml", "chai", "Consumable", 0.6m, 0.53m),
            new("Water", "Nước khoáng thiên nhiên 500ml", "Nước khoáng thiên nhiên đóng chai 500ml", "chai", "Consumable", 0.6m, 0.53m),
            new("Water", "Nước dừa đóng hộp", "Nước dừa tươi đóng hộp bổ sung điện giải", "hộp", "Consumable", 0.4m, 0.35m),
            new("Water", "Bột bù điện giải ORS", "Bột pha bù nước và điện giải cho người mất nước", "gói", "Consumable", 0.05m, 0.025m),
            new("Medical", "Thuốc hạ sốt Paracetamol 500mg", "Thuốc hạ sốt giảm đau cơ bản cho người lớn", "viên", "Consumable", 0.005m, 0.002m),
            new("Medical", "Dầu gió", "Dầu gió xanh dùng xoa bóp giảm đau, chống cảm", "chai", "Consumable", 0.04m, 0.035m),
            new("Medical", "Sắt & Vitamin tổng hợp", "Viên uống bổ sung sắt và vitamin tổng hợp", "viên", "Consumable", 0.005m, 0.002m),
            new("Medical", "Băng gạc y tế vô khuẩn", "Băng gạc vô khuẩn dùng băng bó vết thương", "cuộn", "Consumable", 0.15m, 0.05m),
            new("Medical", "Bông gòn y tế", "Bông gòn y tế vô khuẩn dùng vệ sinh và sơ cứu", "gói", "Consumable", 0.4m, 0.05m),
            new("Medical", "Thuốc kháng sinh Amoxicillin", "Thuốc kháng sinh phổ rộng điều trị nhiễm khuẩn", "viên", "Consumable", 0.005m, 0.002m),
            new("Medical", "Dung dịch sát khuẩn Betadine", "Dung dịch sát khuẩn Povidone-Iodine rửa vết thương", "chai", "Consumable", 0.15m, 0.12m),
            new("Medical", "Khẩu trang y tế 3 lớp", "Khẩu trang y tế dùng một lần, đóng gói vô khuẩn", "chiếc", "Consumable", 0.04m, 0.005m),
            new("Medical", "Bộ sơ cứu cơ bản", "Bộ sơ cứu gồm băng, gạc, kéo, kẹp và thuốc cơ bản", "bộ", "Consumable", 3.0m, 1.5m),
            new("Hygiene", "Băng vệ sinh", "Băng vệ sinh phụ nữ dùng một lần, đóng gói riêng", "miếng", "Consumable", 0.06m, 0.015m),
            new("Hygiene", "Xà phòng diệt khuẩn", "Xà phòng cục diệt khuẩn dùng vệ sinh cá nhân", "bánh", "Consumable", 0.12m, 0.1m),
            new("Hygiene", "Nước rửa tay khô", "Gel rửa tay khô diệt khuẩn nhanh, không cần nước", "chai", "Consumable", 0.3m, 0.28m),
            new("Hygiene", "Khăn ướt kháng khuẩn", "Khăn ướt kháng khuẩn tiện dụng, đóng gói 10 tờ", "gói", "Consumable", 0.25m, 0.1m),
            new("Hygiene", "Kem đánh răng", "Kem đánh răng kích thước nhỏ gọn phù hợp cứu trợ", "tuýp", "Consumable", 0.15m, 0.12m),
            new("Hygiene", "Bàn chải đánh răng", "Bàn chải đánh răng dùng một lần, đóng gói riêng", "chiếc", "Consumable", 0.06m, 0.02m),
            new("Hygiene", "Dầu gội đầu", "Dầu gội đầu gói nhỏ tiện lợi cho cứu trợ", "chai", "Consumable", 0.25m, 0.22m),
            new("Hygiene", "Khăn bông tắm", "Khăn bông tắm cỡ trung dùng vệ sinh cá nhân", "chiếc", "Consumable", 2.5m, 0.35m),
            new("Hygiene", "Giấy vệ sinh", "Giấy vệ sinh cuộn nhỏ tiêu chuẩn", "cuộn", "Consumable", 1.2m, 0.1m),
            new("Hygiene", "Tã dùng một lần", "Tã giấy dùng một lần cho trẻ em hoặc người già", "miếng", "Consumable", 0.5m, 0.06m),
            new("Clothing", "Áo mưa người lớn", "Áo mưa nhựa dùng một lần cho người lớn", "chiếc", "Consumable", 1.5m, 0.25m),
            new("Clothing", "Ủng cao su chống lũ", "Ủng cao su chống nước dùng đi lại trong vùng ngập", "đôi", "Consumable", 6.0m, 1.8m),
            new("Clothing", "Bộ quần áo trẻ em", "Bộ quần áo sạch kích thước trẻ em 3–12 tuổi", "bộ", "Consumable", 2.0m, 0.3m),
            new("Clothing", "Áo ấm người lớn", "Áo khoác giữ ấm dùng trong thời tiết lạnh", "chiếc", "Consumable", 4.0m, 0.7m),
            new("Clothing", "Bộ quần áo người lớn", "Bộ quần áo sạch kích thước người lớn", "bộ", "Consumable", 3.5m, 0.6m),
            new("Clothing", "Bộ quần áo người cao tuổi", "Bộ quần áo thoải mái phù hợp người cao tuổi", "bộ", "Consumable", 3.5m, 0.6m),
            new("Clothing", "Găng tay giữ ấm", "Găng tay len giữ ấm trong thời tiết lạnh", "đôi", "Consumable", 0.3m, 0.08m),
            new("Clothing", "Tất len giữ ấm", "Tất len dày giữ ấm chân trong mùa lạnh", "đôi", "Consumable", 0.2m, 0.06m),
            new("Clothing", "Mũ len", "Mũ len giữ ấm đầu trong thời tiết lạnh", "chiếc", "Consumable", 0.4m, 0.08m),
            new("Clothing", "Áo mưa trẻ em", "Áo mưa nhựa dùng một lần cho trẻ em", "chiếc", "Consumable", 1.0m, 0.18m),
            new("Shelter", "Lều bạt cứu trợ 4 người", "Lều bạt dã chiến sức chứa 4 người, chống nước", "chiếc", "Consumable", 30.0m, 8.0m),
            new("Shelter", "Tấm bạt che mưa đa năng", "Tấm bạt PE chống nước đa năng dùng che mưa nắng", "tấm", "Consumable", 5.0m, 1.5m),
            new("Shelter", "Túi ngủ giữ nhiệt", "Túi ngủ cách nhiệt dùng trong thời tiết lạnh", "chiếc", "Consumable", 10.0m, 1.8m),
            new("Shelter", "Đệm hơi dã chiến", "Đệm hơi gấp gọn dùng ngủ dã chiến", "chiếc", "Consumable", 8.0m, 2.5m),
            new("Shelter", "Màn chống côn trùng", "Màn lưới chống muỗi và côn trùng khi ngủ", "chiếc", "Consumable", 2.0m, 0.4m),
            new("Shelter", "Bộ cọc và dây lều", "Bộ cọc kim loại và dây buộc để dựng lều", "bộ", "Reusable", 3.0m, 2.0m),
            new("Shelter", "Tấm bạt chống thấm", "Tấm bạt PE dày chống thấm nước dùng lót sàn lều", "tấm", "Consumable", 4.0m, 1.2m),
            new("Shelter", "Dây buộc đa năng", "Dây thừng đa năng dùng buộc, cố định vật dụng", "cuộn", "Reusable", 2.0m, 1.5m),
            new("Shelter", "Đèn LED dã chiến", "Đèn LED sạc dùng chiếu sáng dã chiến", "chiếc", "Reusable", 1.0m, 0.35m),
            new("Shelter", "Nến khẩn cấp", "Nến cháy lâu dùng chiếu sáng khi mất điện", "cây", "Consumable", 0.15m, 0.12m),
            new("RepairTools", "Búa đóng đinh", "Búa sắt đóng đinh dùng sửa chữa nhà cửa", "chiếc", "Reusable", 1.5m, 0.5m),
            new("RepairTools", "Đinh các loại", "Bộ đinh sắt các kích cỡ dùng sửa chữa", "gói", "Consumable", 0.3m, 0.5m),
            new("RepairTools", "Cưa tay đa năng", "Cưa tay gấp gọn dùng cắt gỗ và vật liệu", "chiếc", "Reusable", 3.0m, 0.6m),
            new("RepairTools", "Tua vít 2 đầu", "Tua vít 2 đầu dẹt và bake dùng sửa chữa", "chiếc", "Reusable", 0.3m, 0.15m),
            new("RepairTools", "Kìm cắt dây", "Kìm cắt dây thép và dây điện đa năng", "chiếc", "Reusable", 0.5m, 0.3m),
            new("RepairTools", "Băng keo chống thấm", "Băng keo dán chống thấm nước cho mái và tường", "cuộn", "Consumable", 0.2m, 0.15m),
            new("RepairTools", "Dao đa năng dã chiến", "Dao gấp đa năng tích hợp nhiều công cụ", "chiếc", "Reusable", 0.2m, 0.2m),
            new("RepairTools", "Xẻng tay", "Xẻng tay gấp gọn dùng đào đắp trong cứu trợ", "chiếc", "Reusable", 4.0m, 1.2m),
            new("RepairTools", "Bao cát chống lũ", "Bao cát dùng đắp đê ngăn nước lũ tràn", "chiếc", "Reusable", 2.5m, 0.4m),
            new("RepairTools", "Bộ dụng cụ sửa chữa điện cơ bản", "Bộ dụng cụ sửa chữa điện gồm kìm, tua vít, băng keo", "bộ", "Reusable", 4.0m, 2.5m),
            new("RescueEquipment", "Áo phao cứu sinh", "Áo phao tiêu chuẩn phục vụ cứu hộ đường thủy", "chiếc", "Reusable", 8.0m, 1.2m),
            new("RescueEquipment", "Bình lọc nước dã chiến", "Bình lọc nước di động lọc nước bẩn thành nước sạch", "chiếc", "Reusable", 5.0m, 2.0m),
            new("RescueEquipment", "Can đựng nước 10L", "Can nhựa 10 lít chứa và vận chuyển nước sạch", "chiếc", "Reusable", 12.0m, 0.8m),
            new("RescueEquipment", "Túi đựng nước linh hoạt", "Túi nhựa dẻo đựng nước gấp gọn khi không sử dụng", "chiếc", "Reusable", 1.5m, 0.3m),
            new("RescueEquipment", "Nhiệt kế điện tử", "Nhiệt kế điện tử đo thân nhiệt nhanh chóng", "chiếc", "Reusable", 0.1m, 0.05m),
            new("RescueEquipment", "Xuồng cao su cứu hộ", "Xuồng cao su chuyên dụng cho nhiệm vụ cứu hộ lũ", "chiếc", "Reusable", 250.0m, 45.0m),
            new("RescueEquipment", "Dây thừng cứu sinh 30m", "Dây thừng dài 30m chịu lực cao dùng cứu hộ", "cuộn", "Reusable", 6.0m, 3.5m),
            new("RescueEquipment", "Phao tròn cứu sinh", "Phao tròn cứu sinh tiêu chuẩn ném cho nạn nhân", "chiếc", "Reusable", 20.0m, 2.5m),
            new("RescueEquipment", "Máy bơm nước di động", "Máy bơm nước chạy xăng di động hút nước ngập", "chiếc", "Reusable", 60.0m, 25.0m),
            new("RescueEquipment", "Bộ đàm liên lạc dã chiến", "Bộ đàm cầm tay liên lạc tần số UHF/VHF", "chiếc", "Reusable", 0.5m, 0.3m),
            new("RescueEquipment", "Đèn tín hiệu khẩn cấp", "Đèn tín hiệu nhấp nháy cảnh báo khu vực nguy hiểm", "chiếc", "Reusable", 0.8m, 0.4m),
            new("RescueEquipment", "Máy phát điện di động", "Máy phát điện xăng di động công suất nhỏ", "chiếc", "Reusable", 120.0m, 50.0m),
            new("RescueEquipment", "Cáng khiêng thương", "Cáng gấp gọn dùng vận chuyển người bị thương", "chiếc", "Reusable", 30.0m, 7.0m),
            new("RescueEquipment", "Mũ bảo hiểm cứu hộ", "Mũ bảo hiểm chuyên dụng cho cứu hộ viên", "chiếc", "Reusable", 6.0m, 0.6m),
            new("Heating", "Chăn ấm giữ nhiệt", "Chăn dày giữ nhiệt dùng trong thời tiết lạnh", "chiếc", "Consumable", 6.0m, 1.5m),
            new("Heating", "Than tổ ong", "Than tổ ong dùng đốt sưởi ấm hoặc nấu ăn", "viên", "Consumable", 1.2m, 1.0m),
            new("Heating", "Máy sưởi điện mini", "Máy sưởi điện nhỏ gọn công suất thấp", "chiếc", "Consumable", 8.0m, 2.5m),
            new("Heating", "Túi sưởi ấm tay dùng một lần", "Túi sưởi ấm tay phản ứng hóa học dùng một lần", "gói", "Consumable", 0.05m, 0.04m),
            new("Heating", "Bộ quần áo nhiệt", "Bộ đồ lót giữ nhiệt mặc trong thời tiết rét", "bộ", "Consumable", 2.5m, 0.4m),
            new("Heating", "Ấm đun nước du lịch", "Ấm đun nước điện nhỏ gọn tiện dùng dã chiến", "chiếc", "Consumable", 3.0m, 0.8m),
            new("Heating", "Bếp gas du lịch mini", "Bếp gas mini gấp gọn dùng nấu ăn dã chiến", "chiếc", "Consumable", 4.0m, 1.5m),
            new("Heating", "Bình gas mini dã chiến", "Bình gas lon nhỏ dùng cho bếp gas du lịch", "bình", "Consumable", 0.8m, 0.35m),
            new("Heating", "Chăn điện sưởi", "Chăn điện sưởi ấm dùng khi ngủ mùa lạnh", "chiếc", "Consumable", 5.0m, 1.8m),
            new("Heating", "Tấm sưởi ấm bức xạ", "Tấm sưởi hồng ngoại bức xạ di động", "chiếc", "Consumable", 15.0m, 5.0m),
            new("Vehicle", "Xe tải cứu trợ 2.5 tấn", "Xe tải 2.5 tấn vận chuyển hàng cứu trợ", "chiếc", "Reusable", 18000.0m, 3500.0m),
            new("Vehicle", "Xe cứu thương", "Xe chuyên dụng vận chuyển cấp cứu và bệnh nhân", "chiếc", "Reusable", 16000.0m, 3800.0m),
            new("Vehicle", "Xe bán tải 4x4", "Xe bán tải 2 cầu vượt địa hình xấu", "chiếc", "Reusable", 12000.0m, 2200.0m),
            new("Vehicle", "Xe máy địa hình", "Xe máy địa hình đi vào vùng khó tiếp cận", "chiếc", "Reusable", 2500.0m, 150.0m),
            new("Vehicle", "Ca nô cứu hộ", "Ca nô máy chuyên dụng cứu hộ đường thủy", "chiếc", "Reusable", 8000.0m, 800.0m),
            new("Vehicle", "Xe chở hàng nhẹ 1 tấn", "Xe tải nhẹ 1 tấn vận chuyển hàng cứu trợ", "chiếc", "Reusable", 14000.0m, 2500.0m),
            new("Vehicle", "Xe tải đông lạnh 3.5 tấn", "Xe tải đông lạnh bảo quản thực phẩm tươi sống", "chiếc", "Reusable", 20000.0m, 5000.0m),
            new("Vehicle", "Xe khách 16 chỗ", "Xe khách 16 chỗ chở người sơ tán", "chiếc", "Reusable", 15000.0m, 3200.0m),
            new("Vehicle", "Xe cẩu di động", "Xe cẩu di động dọn dẹp đổ nát và vật cản", "chiếc", "Reusable", 20000.0m, 12000.0m),
            new("Vehicle", "Xe chuyên dụng phòng cháy", "Xe chữa cháy chuyên dụng phòng cháy chữa cháy", "chiếc", "Reusable", 18000.0m, 8000.0m),
            new("Others", "Pin dự phòng 10000mAh", "Pin sạc dự phòng 10000mAh sạc điện thoại", "chiếc", "Consumable", 0.25m, 0.22m),
            new("Others", "Cáp sạc đa năng", "Cáp sạc đa đầu Lightning/USB-C/Micro USB", "chiếc", "Consumable", 0.08m, 0.04m),
            new("Others", "Bản đồ địa hình khẩn cấp", "Bản đồ in địa hình khu vực thường xảy ra thiên tai", "tờ", "Consumable", 0.1m, 0.05m),
            new("Others", "Còi báo động khẩn cấp", "Còi thổi báo động và kêu gọi cứu hộ khẩn cấp", "chiếc", "Consumable", 0.02m, 0.015m),
            new("Others", "Kính bảo hộ lao động", "Kính bảo hộ chống bụi và mảnh vỡ khi làm việc", "chiếc", "Reusable", 0.3m, 0.08m),
            new("Others", "Ba lô khẩn cấp", "Ba lô chứa đồ dùng thiết yếu cho tình huống khẩn cấp", "chiếc", "Consumable", 25.0m, 0.8m),
            new("Others", "Sổ tay và bút ghi chép", "Bộ sổ tay và bút bi dùng ghi chép thông tin hiện trường", "bộ", "Consumable", 0.3m, 0.18m),
            new("Others", "Bộ đèn pin đội đầu", "Đèn pin LED đội đầu rọi sáng rảnh tay", "bộ", "Reusable", 0.5m, 0.15m),
            new("Others", "Áo phản quang an toàn", "Áo ghi lê phản quang tăng nhận diện trong đêm", "chiếc", "Reusable", 1.5m, 0.2m),
            new("Others", "Pháo sáng khẩn cấp", "Pháo sáng phát tín hiệu cầu cứu khẩn cấp", "chiếc", "Consumable", 0.25m, 0.15m)
        ];
    }

    private static IReadOnlyList<int> ReliefItemImageIdsInSeedOrder()
    {
        return
        [
            1, 7, 8, 11, 12, 13, 14, 15, 16, 17,
            2, 18, 19, 20, 22, 25, 26,
            3, 9, 10, 27, 28, 29, 30, 32, 33,
            5, 34, 35, 36, 37, 38, 39, 40, 41, 42,
            43, 44, 45, 46, 47, 48, 49, 50, 51, 52,
            53, 54, 55, 56, 57, 58, 59, 60, 61, 62,
            63, 64, 65, 66, 67, 68, 69, 70, 71, 72,
            4, 21, 23, 24, 31, 73, 74, 75, 76, 77, 78, 79, 80, 81,
            6, 82, 83, 84, 85, 86, 87, 88, 89, 90,
            101, 102, 103, 104, 105, 106, 107, 108, 109, 110,
            91, 92, 93, 94, 95, 96, 97, 98, 99, 100
        ];
    }

    private static string? GetReliefItemImageUrl(int id)
    {
        return id switch
        {
            1 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865736/001-mi-tom_n1u4fq.jpg",
            2 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865735/002-nuoc-tinh-khiet_xlky5f.png",
            3 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865755/003-thuoc-ha-sot-paracetamol-500mg_yaeovi.jpg",
            4 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774866312/004-ao-phao-cuu-sinh_ozit6b.jpg",
            5 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865756/005-bang-ve-sinh_yhudge.png",
            6 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865756/006-chan-am-giu-nhiet_ivibn8.png",
            7 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865754/007-sua-bot-tre-em_vzydxc.png",
            8 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865755/008-luong-kho_xhokm0.png",
            9 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865754/009-dau-gio_rbndq6.jpg",
            10 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865755/010-sat-vitamin-tong-hop_rtdjgu.png",
            11 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865754/011-gao-say-kho_urtmri.jpg",
            12 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865754/012-chao-an-lien_rgwjcq.jpg",
            13 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865753/013-banh-mi-kho_xe7rew.jpg",
            14 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865755/014-muoi-tinh_odzyix.png",
            15 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865753/015-duong-cat-trang_vfhuvv.png",
            16 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865753/016-dau-an-thuc-vat_l41nwp.jpg",
            17 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865753/017-thit-hop-dong-goi_xrvcnj.png",
            18 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865753/018-nuoc-loc-binh-20l_xyk8mp.png",
            19 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865754/019-vien-loc-nuoc-khan-cap_jrezrb.jpg",
            20 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865752/020-nuoc-dong-thung-24-chai_ktfzck.jpg",
            21 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865752/021-binh-loc-nuoc-da-chien_gy22py.jpg",
            22 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865752/022-nuoc-khoang-thien-nhien-500ml_fcjxnc.jpg",
            23 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865751/023-can-dung-nuoc-10l_bkqljt.png",
            24 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865751/024-tui-dung-nuoc-linh-hoat_zpizku.jpg",
            25 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865751/025-nuoc-dua-dong-hop_t0ytn2.png",
            26 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865751/026-bot-bu-dien-giai-ors_s47y7a.jpg",
            27 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865751/027-bang-gac-y-te-vo-khuan_c2mkww.jpg",
            28 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865751/028-bong-gon-y-te_jb2euw.png",
            29 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865750/029-thuoc-khang-sinh-amoxicillin_hes4wt.png",
            30 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865750/030-dung-dich-sat-khuan-betadine_zhbkce.jpg",
            31 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865750/031-nhiet-ke-dien-tu_wxgjdw.png",
            32 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865749/032-khau-trang-y-te-3-lop_darfut.jpg",
            33 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865751/033-bo-so-cuu-co-ban_ws83xn.png",
            34 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865749/034-xa-phong-diet-khuan_g09ho0.png",
            35 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865749/035-nuoc-rua-tay-kho_bxhmvl.jpg",
            36 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865749/036-khan-uot-khang-khuan_wwoh14.png",
            37 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865748/037-kem-danh-rang_s2ibzl.jpg",
            38 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865750/038-ban-chai-danh-rang_vd42ax.png",
            39 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865748/039-dau-goi-dau_o9njdq.jpg",
            40 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865748/040-khan-bong-tam_o94plx.png",
            41 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865748/041-giay-ve-sinh_c3fryk.jpg",
            42 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865748/042-ta-dung-mot-lan_yixozm.jpg",
            43 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865747/043-ao-mua-nguoi-lon_fc7kry.jpg",
            44 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865747/044-ung-cao-su-chong-lu_lz9qbw.jpg",
            45 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865747/045-bo-quan-ao-tre-em_n4agu9.jpg",
            46 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865747/046-ao-am-nguoi-lon_ma6thc.jpg",
            47 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865747/047-bo-quan-ao-nguoi-lon_umzueu.png",
            48 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865747/048-bo-quan-ao-nguoi-cao-tuoi_por2xe.jpg",
            49 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865746/049-gang-tay-giu-am_k56rfm.jpg",
            50 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865746/050-tat-len-giu-am_ov0jjd.jpg",
            51 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865746/051-mu-len_wzipsi.jpg",
            52 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865757/052-ao-mua-tre-em_b0mocf.jpg",
            53 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865746/053-leu-bat-cuu-tro-4-nguoi_qj8w9i.png",
            54 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865746/054-tam-bat-che-mua-da-nang_xvvydi.jpg",
            55 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865746/055-tui-ngu-giu-nhiet_mnhbww.jpg",
            56 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865745/056-dem-hoi-da-chien_ns7izi.jpg",
            57 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865745/057-man-chong-con-trung_iip3fn.jpg",
            58 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865745/058-bo-coc-va-day-leu_ywukij.jpg",
            59 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865745/059-tam-bat-chong-tham_ensdzn.jpg",
            60 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865745/060-day-buoc-da-nang_mpzo8n.jpg",
            61 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865745/061-den-led-da-chien_hcylgj.jpg",
            62 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865745/062-nen-khan-cap_fwzazj.png",
            63 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865744/063-bua-dong-dinh_ulqde0.jpg",
            64 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865744/064-dinh-cac-loai_k7fsm9.jpg",
            65 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865744/065-cua-tay-da-nang_jopzf5.jpg",
            66 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865744/066-tua-vit-2-dau_tzzrzx.jpg",
            67 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865743/067-kim-cat-day_tiq6jt.jpg",
            68 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865743/068-bang-keo-chong-tham_bbctyd.jpg",
            69 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865742/069-dao-da-nang-da-chien_n68ore.jpg",
            70 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865742/070-xeng-tay_ktfrdj.jpg",
            71 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865742/071-bao-cat-chong-lu_cvey61.jpg",
            72 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865741/072-bo-dung-cu-sua-chua-dien-co-ban_k2peyh.jpg",
            73 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865741/073-xuong-cao-su-cuu-ho_t3gcxt.jpg",
            74 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865740/074-day-thung-cuu-sinh-30m_nepsc3.png",
            75 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865740/075-phao-tron-cuu-sinh_fosz4i.jpg",
            76 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865739/076-may-bom-nuoc-di-dong_npf0tr.jpg",
            77 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865740/077-bo-dam-lien-lac-da-chien_kwbfsm.jpg",
            78 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865739/078-den-tin-hieu-khan-cap_o3frpt.jpg",
            79 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865738/078-den-tin-hieu-khan-cap_yp3mui.jpg",
            80 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865738/080-cang-khieng-thuong_xszlmj.jpg",
            81 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865737/081-mu-bao-hiem-cuu-ho_qetnbw.jpg",
            82 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865737/082-than-to-ong_m7sdry.jpg",
            83 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865738/083-may-suoi-dien-mini_hy0wg4.png",
            84 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865736/084-tui-suoi-am-tay-dung-mot-lan_sadxtb.jpg",
            85 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865736/085-bo-quan-ao-nhiet_wxsmmj.jpg",
            86 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865736/086-am-dun-nuoc-du-lich_vbh2ap.jpg",
            87 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865736/087-bep-gas-du-lich-mini_zeyjrk.jpg",
            88 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865735/088-binh-gas-mini-da-chien_yeapzn.jpg",
            89 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865734/089-chan-dien-suoi_kvul8o.jpg",
            90 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865744/090-tam-suoi-am-buc-xa_tysxho.png",
            91 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865743/091-pin-du-phong-10000mah_gczx45.jpg",
            92 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865743/092-cap-sac-da-nang_knsvuy.jpg",
            93 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865742/093-ban-do-dia-hinh-khan-cap_pm5zkt.jpg",
            94 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865741/094-coi-bao-dong-khan-cap_ukvhal.png",
            95 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865741/095-kinh-bao-ho-lao-dong_wl8n1f.jpg",
            96 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865742/096-ba-lo-khan-cap_jn7icq.jpg",
            97 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865741/097-so-tay-va-but-ghi-chep_h9lums.jpg",
            98 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865740/098-bo-den-pin-doi-dau_ucnidx.jpg",
            99 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865739/099-ao-phan-quang-an-toan_trpgia.jpg",
            100 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865738/100-phao-sang-khan-cap_t0nxwi.jpg",
            101 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865738/101-xe-tai-cuu-tro-2-5-tan_ifxbqk.jpg",
            102 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865738/102-xe-cuu-thuong_zqevrt.png",
            103 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865739/103-xe-ban-tai-4x4_wrs2t4.png",
            104 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865755/104-xe-may-dia-hinh_xphh0x.png",
            105 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865737/105-ca-no-cuu-ho_lzudkx.jpg",
            106 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865737/106-xe-cho-hang-nhe-1-tan_rrmaie.png",
            107 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865736/107-xe-tai-dong-lanh-3-5-tan_ttxps8.jpg",
            108 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865735/108-xe-khach-16-cho_h3tjcc.jpg",
            109 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865735/109-xe-cau-di-dong_xcphgy.jpg",
            110 => "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774865735/110-xe-chuyen-dung-phong-chay_xoomtb.jpg",
            _ => null
        };
    }

    private static IReadOnlyList<string> TargetGroupNamesFor(ItemTemplate template)
    {
        var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static bool HasAny(string value, params string[] patterns) =>
            patterns.Any(pattern => value.Contains(pattern, StringComparison.OrdinalIgnoreCase));

        void Add(params string[] names)
        {
            foreach (var name in names)
            {
                groups.Add(name);
            }
        }

        switch (template.CategoryCode)
        {
            case "FOOD":
            case "WATER":
            case "MEDICINE":
            case "HYGIENE":
            case "CLOTHING":
            case "SHELTER":
            case "HEATING":
            case "OTHERS":
                Add("Adult");
                break;
            case "REPAIR_TOOLS":
            case "RESCUE_EQUIPMENT":
            case "VEHICLE":
                Add("Rescuer");
                break;
        }

        if (HasAny(template.Name, "trẻ em"))
        {
            Add("Children");
        }

        if (HasAny(template.Name, "người cao tuổi"))
        {
            Add("Elderly");
        }

        if (HasAny(template.Name, "Băng vệ sinh", "Sắt & Vitamin"))
        {
            Add("Pregnant");
        }

        if (HasAny(template.Name, "Cháo ăn liền", "Chăn ấm giữ nhiệt"))
        {
            Add("Children", "Elderly", "Pregnant");
        }

        if (HasAny(template.Name, "Nước tinh khiết", "Bột bù điện giải ORS"))
        {
            Add("Children", "Elderly", "Pregnant", "Rescuer");
        }

        if (template.Name == "Gạo sấy khô")
        {
            Add("Elderly", "Pregnant", "Rescuer");
        }

        if (template.Name == "Tã dùng một lần")
        {
            Add("Children", "Elderly");
        }

        if (template.CategoryCode == "FOOD" && HasAny(template.Name, "Mì tôm", "Lương khô", "Bánh mì khô", "Thịt hộp"))
        {
            Add("Rescuer");
        }

        if (template.CategoryCode == "WATER" && HasAny(template.Name, "Viên lọc nước khẩn cấp"))
        {
            Add("Rescuer");
        }

        if (template.CategoryCode == "MEDICINE" && HasAny(template.Name, "Băng gạc", "Bông gòn", "Betadine", "Khẩu trang", "Bộ sơ cứu"))
        {
            Add("Rescuer");
        }

        if (template.CategoryCode == "HYGIENE" && HasAny(template.Name, "Nước rửa tay khô", "Khăn ướt kháng khuẩn"))
        {
            Add("Rescuer");
        }

        if (template.CategoryCode == "CLOTHING" && HasAny(template.Name, "Áo mưa người lớn", "Ủng cao su chống lũ"))
        {
            Add("Rescuer");
        }

        if (template.CategoryCode == "SHELTER" && (template.ItemType == "Reusable" || HasAny(template.Name, "Lều bạt", "Tấm bạt chống thấm", "Nến khẩn cấp")))
        {
            Add("Rescuer");
        }

        if (template.CategoryCode == "HEATING" && HasAny(template.Name, "Túi sưởi", "Bếp gas", "Bình gas"))
        {
            Add("Rescuer");
        }

        if (template.CategoryCode == "OTHERS" && HasAny(template.Name, "Pin dự phòng", "Cáp sạc", "Bản đồ", "Còi báo động", "Ba lô", "Bộ đèn pin", "Áo phản quang", "Kính bảo hộ", "Pháo sáng"))
        {
            Add("Rescuer");
        }

        if (groups.Count == 0)
        {
            Add("Adult");
        }

        return groups.ToList();
    }

    private static double PriorityScore(string priority, int index)
    {
        return priority switch
        {
            "Critical" => 88 + index % 12,
            "High" => 68 + index % 15,
            "Medium" => 42 + index % 18,
            _ => 20 + index % 18
        };
    }

    private static string MissionType(int index, string? severity)
    {
        if (severity == "Critical" && index % 2 == 0)
        {
            return "Mixed";
        }

        var types = new[] { "Rescue", "Medical", "Supply", "Mixed" };
        return types[index % types.Length];
    }

    private RescueTeam TeamForMission(DemoSeedContext seed, int missionIndex, int teamOffset)
    {
        var missionType = seed.Missions[missionIndex].MissionType;
        var required = missionType switch
        {
            "Medical" => "Medical",
            "Supply" => "Transportation",
            "Mixed" => "Mixed",
            _ => "Rescue"
        };
        var candidates = seed.RescueTeams
            .Where(t => !IsHueStadiumReserveTeam(t) && t.TeamType == required && t.Status is "Available" or "Gathering")
            .ToList();

        if (candidates.Count > 0)
        {
            return candidates[(missionIndex + teamOffset) % candidates.Count];
        }

        candidates = seed.RescueTeams
            .Where(t => !IsHueStadiumReserveTeam(t) && t.TeamType == required && t.Status != "Disbanded" && t.Status != "Unavailable")
            .ToList();

        if (candidates.Count > 0)
        {
            return candidates[(missionIndex + teamOffset) % candidates.Count];
        }

        candidates = seed.RescueTeams
            .Where(t => !IsHueStadiumReserveTeam(t))
            .ToList();
        return candidates[(missionIndex + teamOffset) % candidates.Count];
    }

    private static void SyncRescueTeamStatusesFromAssignments(DemoSeedContext seed)
    {
        var activeMissionTeamsByRescueTeam = seed.MissionTeams
            .Where(team => team.RescuerTeamId.HasValue && team.UnassignedAt is null && team.Status != "Cancelled")
            .GroupBy(team => team.RescuerTeamId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (var rescueTeam in seed.RescueTeams)
        {
            if (rescueTeam.Status is "Disbanded" or "Unavailable" or "Stuck")
            {
                continue;
            }

            if (!activeMissionTeamsByRescueTeam.TryGetValue(rescueTeam.Id, out var missionTeams))
            {
                rescueTeam.Status = rescueTeam.Status == "Gathering" ? "Gathering" : "Available";
                continue;
            }

            rescueTeam.Status = missionTeams.Any(team => team.Status == "InProgress")
                ? "OnMission"
                : missionTeams.Any(team => team.Status == "Assigned")
                    ? "Assigned"
                    : "Available";
        }
    }

    /// <summary>
    /// Post-processing: adjusts activities at Kho Huế (Depots[0]) to fixed demo-test statuses
    /// so that manager01 always has data for upcoming-returns, upcoming-pickups, confirm-return
    /// and confirm-pickup endpoints after every fresh seed.
    /// </summary>
    private async Task SeedTestActivityStatusesAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        if (seed.Depots.Count == 0) return;
        var hueDepotId = seed.Depots[0].Id; // Uỷ Ban MTTQVN Tỉnh Thừa Thiên Huế (manager@resq.vn)
        var onGoingMissionIds = seed.Missions
            .Where(mission => mission.Status == "OnGoing")
            .Select(mission => mission.Id)
            .ToHashSet();
        var inProgressMissionTeamIds = seed.MissionTeams
            .Where(team => team.Status == "InProgress")
            .Select(team => team.Id)
            .ToHashSet();

        // 1. Three RETURN_SUPPLIES → PendingConfirmation (for manager01 upcoming-returns + confirm-return)
        var returnActivities = seed.MissionActivities
            .Where(a => a.ActivityType == "RETURN_SUPPLIES"
                     && a.Status == "Planned"
                     && a.MissionId.HasValue
                     && onGoingMissionIds.Contains(a.MissionId.Value)
                     && a.MissionTeamId.HasValue
                     && inProgressMissionTeamIds.Contains(a.MissionTeamId.Value))
            .OrderBy(a => a.AssignedAt)
            .ThenBy(a => a.Id)
            .Take(3)
            .ToList();
        EnsureManagerUpcomingReturnFixtures(seed, seed.Depots[0], returnActivities);

        // 2. One COLLECT_SUPPLIES → OnGoing  (for upcoming-pickups + confirm-pickup)
        var pickupActivity = seed.MissionActivities
            .FirstOrDefault(a => a.DepotId == hueDepotId
                              && a.ActivityType == "COLLECT_SUPPLIES"
                              && a.Status == "Succeed");
        if (pickupActivity != null)
        {
            pickupActivity.Status = "OnGoing";
            pickupActivity.CompletedAt = null;
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "SeedTestActivityStatuses: returnActivityIds={ReturnIds} -> PendingConfirmation; pickupActivityId={PickupId} -> OnGoing (hueDepotId={DepotId})",
            string.Join(",", returnActivities.Select(a => a.Id)), pickupActivity?.Id, hueDepotId);
    }

    private static void EnsureManagerUpcomingReturnFixtures(
        DemoSeedContext seed,
        Depot hueDepot,
        IReadOnlyList<MissionActivity> returnActivities)
    {
        if (returnActivities.Count < 3)
        {
            throw new InvalidOperationException(
                $"Runtime demo seed requires at least 3 planned RETURN_SUPPLIES activities for depot #{hueDepot.Id}.");
        }

        var reusableUnitGroups = seed.ReusableItems
            .Where(item => item.Id > 30
                && item.DepotId == hueDepot.Id
                && string.Equals(item.Status, nameof(ReusableItemStatus.Available), StringComparison.Ordinal)
                && item.ItemModelId.HasValue
                && seed.ItemModels.Any(model =>
                    model.Id == item.ItemModelId.Value
                    && string.Equals(model.ItemType, nameof(ItemType.Reusable), StringComparison.OrdinalIgnoreCase)))
            .GroupBy(item => item.ItemModelId!.Value)
            .Where(group => group.Count() >= 2)
            .OrderBy(group => group.Key)
            .ToList();

        if (reusableUnitGroups.Count < 2)
        {
            throw new InvalidOperationException(
                $"Runtime demo seed requires reusable units for upcoming return fixtures at depot #{hueDepot.Id}.");
        }

        var reusableOnlyUnits = reusableUnitGroups[0].OrderBy(item => item.Id).Take(2).ToList();
        var mixedReusableUnit = reusableUnitGroups[1].OrderBy(item => item.Id).First();

        MarkUnitsInUse(reusableOnlyUnits.Concat([mixedReusableUnit]), seed.AnchorUtc);

        ConfigureReturnFixture(
            returnActivities[0],
            "Demo manager01 - trả thiết bị tái sử dụng",
            "Đơn demo manager01: đội trả thiết bị tái sử dụng về kho Huế, có serial cụ thể.",
            hueDepot,
            [
                BuildReusableReturnItem(seed, reusableOnlyUnits)
            ],
            assignedOffsetMinutes: 0);

        ConfigureReturnFixture(
            returnActivities[1],
            "Demo manager01 - trả vật phẩm tiêu hao theo lô",
            "Đơn demo manager01: đội trả vật phẩm tiêu hao dư thừa về kho Huế theo đúng lô FEFO.",
            hueDepot,
            BuildConsumableReturnItems(seed, hueDepot.Id,
            [
                ("Mì tôm", 20),
                ("Nước tinh khiết", 80),
                ("Thuốc hạ sốt Paracetamol 500mg", 120)
            ]),
            assignedOffsetMinutes: 20);

        var mixedItems = BuildConsumableReturnItems(seed, hueDepot.Id,
        [
            ("Nước tinh khiết", 12),
            ("Chăn ấm giữ nhiệt", 5)
        ]);
        mixedItems.Add(BuildReusableReturnItem(seed, [mixedReusableUnit]));

        ConfigureReturnFixture(
            returnActivities[2],
            "Demo manager01 - trả vật phẩm tiêu hao và thiết bị tái sử dụng",
            "Đơn demo manager01: đội trả cả vật phẩm tiêu hao theo lô và thiết bị tái sử dụng có serial.",
            hueDepot,
            mixedItems,
            assignedOffsetMinutes: 40);
    }

    private static void ConfigureReturnFixture(
        MissionActivity activity,
        string targetName,
        string description,
        Depot hueDepot,
        List<SupplyToCollectDto> items,
        int assignedOffsetMinutes)
    {
        var assignedAt = activity.AssignedAt ?? activity.Mission?.StartTime ?? activity.Mission?.CreatedAt;

        activity.ActivityType = "RETURN_SUPPLIES";
        activity.Description = description;
        activity.Target = Json(new { location = targetName, purpose = "manager01_upcoming_return_fixture" });
        activity.Items = Json(items);
        activity.TargetLocation = hueDepot.Location;
        activity.Status = "PendingConfirmation";
        activity.CompletedAt = null;
        activity.AssignedAt = assignedAt?.AddMinutes(assignedOffsetMinutes);
        activity.Priority = "Medium";
        activity.EstimatedTime = 30;
        activity.DepotId = hueDepot.Id;
        activity.DepotName = hueDepot.Name;
        activity.DepotAddress = hueDepot.Address;
    }

    private static List<SupplyToCollectDto> BuildConsumableReturnItems(
        DemoSeedContext seed,
        int depotId,
        IReadOnlyList<(string ItemName, int Quantity)> requests)
    {
        return requests
            .Select(request => BuildConsumableReturnItem(seed, depotId, request.ItemName, request.Quantity))
            .ToList();
    }

    private static SupplyToCollectDto BuildConsumableReturnItem(
        DemoSeedContext seed,
        int depotId,
        string itemName,
        int quantity)
    {
        var itemModel = seed.ItemModels.Single(model =>
            string.Equals(model.Name, itemName, StringComparison.OrdinalIgnoreCase));
        var inventory = seed.Inventories.Single(inventory =>
            inventory.DepotId == depotId && inventory.ItemModelId == itemModel.Id);

        var remaining = quantity;
        var allocations = new List<SupplyExecutionLotDto>();
        foreach (var lot in seed.Lots
            .Where(lot => lot.SupplyInventoryId == inventory.Id && lot.RemainingQuantity > 0)
            .OrderBy(lot => lot.ExpiredDate ?? DateTime.MaxValue)
            .ThenBy(lot => lot.ReceivedDate ?? DateTime.MaxValue)
            .ThenBy(lot => lot.Id))
        {
            var take = Math.Min(remaining, lot.RemainingQuantity);
            if (take <= 0)
            {
                continue;
            }

            allocations.Add(new SupplyExecutionLotDto
            {
                LotId = lot.Id,
                QuantityTaken = take,
                ReceivedDate = lot.ReceivedDate,
                ExpiredDate = lot.ExpiredDate,
                RemainingQuantityAfterExecution = Math.Max(0, lot.RemainingQuantity - take)
            });

            remaining -= take;
            if (remaining == 0)
            {
                break;
            }
        }

        if (remaining > 0)
        {
            throw new InvalidOperationException(
                $"Runtime demo seed cannot allocate {quantity} units of '{itemName}' from depot #{depotId} lots.");
        }

        return new SupplyToCollectDto
        {
            ItemId = itemModel.Id,
            ItemName = itemModel.Name ?? itemName,
            ImageUrl = itemModel.ImageUrl,
            Quantity = quantity,
            Unit = itemModel.Unit,
            ExpectedReturnLotAllocations = allocations
        };
    }

    private static SupplyToCollectDto BuildReusableReturnItem(
        DemoSeedContext seed,
        IReadOnlyList<ReusableItem> units)
    {
        var itemModelId = units.Select(unit => unit.ItemModelId).Distinct().Single()
            ?? throw new InvalidOperationException("Reusable return fixture unit is missing ItemModelId.");
        var itemModel = seed.ItemModels.Single(model => model.Id == itemModelId);

        return new SupplyToCollectDto
        {
            ItemId = itemModel.Id,
            ItemName = itemModel.Name ?? $"Thiết bị #{itemModel.Id}",
            ImageUrl = itemModel.ImageUrl,
            Quantity = units.Count,
            Unit = itemModel.Unit,
            ExpectedReturnUnits = units
                .OrderBy(unit => unit.Id)
                .Select(unit => new SupplyExecutionReusableUnitDto
                {
                    ReusableItemId = unit.Id,
                    ItemModelId = itemModel.Id,
                    ItemName = itemModel.Name ?? $"Thiết bị #{itemModel.Id}",
                    SerialNumber = unit.SerialNumber,
                    Condition = unit.Condition,
                    Note = unit.Note
                })
                .ToList()
        };
    }

    private static void MarkUnitsInUse(IEnumerable<ReusableItem> units, DateTime updatedAt)
    {
        foreach (var unit in units)
        {
            unit.Status = ReusableItemStatus.InUse.ToString();
            unit.UpdatedAt = updatedAt;
            unit.Note = "Đang được đội giữ để trả về kho trong đơn RETURN_SUPPLIES demo manager01.";
        }
    }

    private static string ActivityType(int step, int total, string? missionType)
    {
        if (step == 1)
        {
            return "COLLECT_SUPPLIES";
        }

        if (step == total)
        {
            return "RETURN_SUPPLIES";
        }

        if (step == 2 && missionType is "Supply" or "Mixed")
        {
            return "DELIVER_SUPPLIES";
        }

        if (missionType == "Medical")
        {
            return "MEDICAL_AID";
        }

        return step % 2 == 0 ? "EVACUATE" : "RESCUE";
    }

    private static string ActivityStatusFor(string? missionStatus, int step, int total)
    {
        return missionStatus switch
        {
            "Completed" => "Succeed",
            "OnGoing" => step == 1 ? "Succeed" : step == total ? "Planned" : "OnGoing",
            "Incompleted" => step == total ? "Failed" : "Succeed",
            _ => "Planned"
        };
    }

    private static string ActivityDescription(string type, string? depotName, string? sosMessage)
    {
        return type switch
        {
            "COLLECT_SUPPLIES" => $"Di chuyển đến {depotName}, nhận nước uống, thuốc và áo phao.",
            "DELIVER_SUPPLIES" => "Giao vật phẩm cho hộ dân theo danh sách SOS.",
            "RETURN_SUPPLIES" => $"Hoàn trả áo phao, bộ đàm và dây cứu sinh về {depotName}.",
            "MEDICAL_AID" => "Sơ cứu tại chỗ, kiểm tra huyết áp và chuyển tuyến nếu cần.",
            "EVACUATE" => "Đưa người già, trẻ em ra điểm tránh trú an toàn.",
            _ => sosMessage ?? "Tiếp cận hiện trường và hỗ trợ cứu hộ."
        };
    }

    private static string IncidentDescription(int index)
    {
        var descriptions = new[]
        {
            "Xuồng bị kẹt rác ở chân cầu, cần hỗ trợ kéo ra.",
            "Đường vào khu dân cư nước chảy xiết, đội tạm dừng chờ điều phối.",
            "Một rescuer bị trượt chân xây xát nhẹ.",
            "Phát hiện thêm hộ dân bị cô lập phía sau trường mầm non.",
            "Bộ đàm mất tín hiệu trong 15 phút do mưa lớn."
        };
        return descriptions[index % descriptions.Length];
    }

    private static string IncidentType(int index)
    {
        var types = new[] { "VehicleIssue", "UnsafeRoute", "RescuerInjury", "AdditionalVictimsFound", "CommunicationLost" };
        return types[index % types.Length];
    }

    private static string SosUpdateContent(string? status)
    {
        return status switch
        {
            "Resolved" => "Đội cứu hộ xác nhận đã hỗ trợ xong và cập nhật an toàn.",
            "InProgress" => "Đội cứu hộ đang trên đường, ETA khoảng 20 phút.",
            "Assigned" => "Đã phân công đội phụ trách tiếp cận hiện trường.",
            "Cancelled" => "Yêu cầu đã hủy sau khi xác minh an toàn.",
            _ => "Đang chờ điều phối viên xác nhận thêm thông tin."
        };
    }

    private static string ChatMessage(int index, string? status)
    {
        var active = status == "CoordinatorActive";
        var messages = new[]
        {
            "Hệ thống đã ghi nhận yêu cầu hỗ trợ.",
            "Tôi đã đọc thông tin SOS, bạn hãy giữ điện thoại khô và bật âm lượng.",
            "Nhà em còn một bà cụ không đi lại được, nước đang lên nhanh.",
            active ? "Đội cứu hộ đang di chuyển từ điểm tập kết gần nhất." : "Tôi đang chờ điều phối viên phản hồi.",
            "Nếu có thể, hãy tập trung mọi người ở vị trí cao nhất trong nhà.",
            "Gia đình còn nước uống khoảng nửa ngày.",
            "Đã bổ sung nhu cầu nước uống và thuốc vào ghi chú mission.",
            "Có trẻ nhỏ nên cần áo phao cỡ nhỏ khi tiếp cận.",
            "Tín hiệu hơi yếu, tôi sẽ gửi vị trí lại.",
            "Đã nhận vị trí, sai số khoảng dưới 30m.",
            "Khi thấy đội cứu hộ, hãy dùng đèn pin hoặc khăn sáng màu để báo hiệu.",
            "Cảm ơn, gia đình sẽ chờ ở tầng hai.",
            "Cuộc hội thoại được lưu để điều phối tiếp theo.",
            "Ảnh hiện trường: https://cdn.resq.vn/chat/flood-demo.jpg"
        };
        return messages[index % messages.Length];
    }

    private static string SupplyRequestNote(int index)
    {
        var notes = new[]
        {
            "Thiếu nước uống và thuốc hạ sốt cho đợt lũ Quảng Điền.",
            "Cần bổ sung áo phao, dây cứu sinh cho đội xuồng.",
            "Kho địa phương gần cạn lương khô và sữa trẻ em.",
            "Xin điều chuyển bộ đàm và pin dự phòng trước bão.",
            "Cần máy phát điện và đèn sạc cho điểm tránh trú."
        };
        return notes[index % notes.Length];
    }

    private static string OrganizationName(int index)
    {
        var names = new[]
        {
            "Nhóm thiện nguyện Hướng về miền Trung",
            "Công ty nước sạch Sông Hương",
            "Hội Chữ thập đỏ Đà Nẵng",
            "Quỹ cộng đồng Bạch Mã",
            "Câu lạc bộ xe bán tải miền Trung",
            "Công ty thiết bị cứu hộ An Tâm",
            "Nhóm Bếp ấm vùng lũ"
        };
        return names[index % names.Length] + (index >= 7 ? $" {index + 1}" : "");
    }

    private static string SupplierName(int index)
    {
        var suppliers = new[]
        {
            "Công ty TNHH Thiết bị cứu hộ An Tâm",
            "Công ty CP Nước uống Sông Hương",
            "Nhà thuốc Trung tâm Huế",
            "Công ty TNHH Lương thực miền Trung",
            "Công ty CP Vật tư y tế Đà Nẵng"
        };
        return suppliers[index % suppliers.Length];
    }

    private static string CampaignName(int index)
    {
        var names = new[]
        {
            "Chiến dịch hỗ trợ bão Noru miền Trung",
            "Chiến dịch lũ sớm Huế - Quảng Trị",
            "Chiến dịch tiếp sức vùng sạt lở Trà My",
            "Chiến dịch nước sạch sau lũ Quảng Ngãi",
            "Chiến dịch áo phao cho vùng ngập sâu"
        };
        return names[index % names.Length] + $" #{index + 1}";
    }

    private sealed class ConsumableInventoryHistoryPlan
    {
        public required SupplyInventory Inventory { get; init; }
        public required ItemModel ItemModel { get; init; }
        public required SupplyInventoryLot BaseLot { get; init; }
        public List<SupplyInventoryLot> SupplementalImportLots { get; init; } = [];
        public required Guid PerformedBy { get; init; }
        public int FinalQuantity => Inventory.Quantity ?? 0;
        public List<ConsumableOutboundEvent> OutboundEvents { get; } = [];
        public List<ConsumableInboundTransferEvent> InboundTransfers { get; } = [];
        public List<ConsumableAdjustmentEvent> Adjustments { get; } = [];
    }

    private sealed class ConsumableOutboundEvent
    {
        public required string ActionType { get; init; }
        public required string SourceType { get; init; }
        public int? SourceId { get; init; }
        public required int Quantity { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required Guid PerformedBy { get; init; }
        public int? MissionId { get; init; }
        public required string Note { get; init; }
    }

    private sealed class ConsumableInboundTransferEvent
    {
        public required int Quantity { get; init; }
        public int? SourceId { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required Guid PerformedBy { get; init; }
        public DateTime? ReceivedDate { get; init; }
        public DateTime? ExpiredDate { get; init; }
        public required string Note { get; init; }
    }

    private sealed class ConsumableAdjustmentEvent
    {
        public required int Quantity { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required Guid PerformedBy { get; init; }
        public required string Note { get; init; }
    }

    private sealed record SeedArea(string Code, string Province, string Ward, string Address, double Lat, double Lon);
    private sealed record SeedCoordinate(double Lat, double Lon);

    private sealed record RelativeProfileSeed(
        string DisplayName,
        string PhoneNumber,
        string PersonType,
        string Gender,
        IReadOnlyList<string> Tags,
        string? MedicalBaselineNote,
        string? SpecialNeedsNote,
        string? SpecialDietNote,
        string MedicalProfileJson);

    private sealed record HueStadiumClusterScenario(
        string Code,
        double Latitude,
        double Longitude,
        double RadiusKm,
        string SeverityLevel,
        string WaterLevel,
        int VictimEstimated,
        int ChildrenCount,
        int ElderlyCount,
        double MedicalUrgencyScore,
        string Status,
        DateTime LocalCreatedAt);

    private sealed record HueStadiumSosScenario(
        int ClusterIndex,
        double Latitude,
        double Longitude,
        string Address,
        string SosType,
        string Status,
        string PriorityLevel,
        double PriorityScore,
        string Situation,
        bool CanMove,
        bool HasInjured,
        bool NeedMedical,
        bool OthersAreStable,
        string AdditionalDescription,
        string RawMessage,
        string Network,
        int BatteryPercentage,
        bool IsSentOnBehalf,
        int VictimIndex,
        int ReporterIndex,
        int CoordinatorIndex,
        DateTime LocalCreatedAt,
        IReadOnlyList<HueStadiumVictimScenario> Victims,
        IReadOnlyList<string> GroupNeeds);

    private sealed record HueStadiumVictimScenario(
        string PersonId,
        string PersonType,
        string CustomName,
        string IncidentStatus,
        IReadOnlyList<string> PersonalNeeds);

    private sealed record ItemTemplate(
        string CategoryCode,
        string Name,
        string Description,
        string Unit,
        string ItemType,
        decimal Volume,
        decimal Weight);
}
