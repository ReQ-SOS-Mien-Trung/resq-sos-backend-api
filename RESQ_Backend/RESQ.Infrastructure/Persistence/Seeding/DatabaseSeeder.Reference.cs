using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Constants;
using RESQ.Infrastructure.Entities.Emergency;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Entities.System;
using LogisticsTargetGroup = RESQ.Infrastructure.Entities.Logistics.TargetGroup;

namespace RESQ.Infrastructure.Persistence.Seeding;

public sealed partial class DatabaseSeeder
{
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
}
