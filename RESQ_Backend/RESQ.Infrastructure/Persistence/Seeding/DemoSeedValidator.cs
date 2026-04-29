using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Persistence.Seeding;

public sealed class DemoSeedValidator
{
    private static readonly HashSet<string> Priorities = new(StringComparer.Ordinal)
    {
        "Low", "Medium", "High", "Critical"
    };

    private static readonly HashSet<string> SosTypes = new(StringComparer.Ordinal)
    {
        "Rescue", "Relief", "Both"
    };

    private static readonly HashSet<string> SosStatuses = new(StringComparer.Ordinal)
    {
        "Pending", "Assigned", "InProgress", "Incident", "Resolved", "Cancelled"
    };

    private static readonly HashSet<string> SosClusterStatuses = new(StringComparer.Ordinal)
    {
        "Pending", "Suggested", "InProgress", "Completed"
    };

    private static readonly HashSet<string> MissionTypes = new(StringComparer.Ordinal)
    {
        "Rescue", "Medical", "Supply", "Mixed"
    };

    private static readonly HashSet<string> MissionStatuses = new(StringComparer.Ordinal)
    {
        "Planned", "OnGoing", "Completed", "Incompleted"
    };

    private static readonly HashSet<string> ActivityStatuses = new(StringComparer.Ordinal)
    {
        "Planned", "OnGoing", "Succeed", "PendingConfirmation", "Failed", "Cancelled"
    };

    private static readonly HashSet<string> IncidentStatuses = new(StringComparer.Ordinal)
    {
        "Reported", "InProgress", "Resolved"
    };

    public async Task<IReadOnlyList<string>> ValidateAsync(ResQDbContext db, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        var badPriorities = await db.SosRequests
            .Where(s => s.PriorityLevel != null && !Priorities.Contains(s.PriorityLevel))
            .Select(s => s.PriorityLevel!)
            .Distinct()
            .ToListAsync(cancellationToken);
        AddInvalidValues(errors, "sos_requests.priority_level", badPriorities);

        var badSosStatuses = await db.SosRequests
            .Where(s => s.Status != null && !SosStatuses.Contains(s.Status))
            .Select(s => s.Status!)
            .Distinct()
            .ToListAsync(cancellationToken);
        AddInvalidValues(errors, "sos_requests.status", badSosStatuses);

        var badSosClusterStatuses = await db.SosClusters
            .Where(c => !SosClusterStatuses.Contains(c.Status))
            .Select(c => c.Status)
            .Distinct()
            .ToListAsync(cancellationToken);
        AddInvalidValues(errors, "sos_clusters.status", badSosClusterStatuses);

        var completedClustersWithOpenSos = await db.SosClusters
            .Where(c => c.Status == "Completed")
            .Where(c => db.SosRequests.Any(s => s.ClusterId == c.Id && s.Status != "Resolved"))
            .Select(c => c.Id)
            .Take(20)
            .ToListAsync(cancellationToken);
        if (completedClustersWithOpenSos.Count > 0)
        {
            errors.Add($"Completed SOS clusters contain non-resolved SOS requests: {string.Join(", ", completedClustersWithOpenSos)}.");
        }

        var resolvedClustersNotCompleted = await db.SosClusters
            .Where(c => c.Status != "Completed")
            .Where(c => db.SosRequests.Any(s => s.ClusterId == c.Id))
            .Where(c => !db.SosRequests.Any(s => s.ClusterId == c.Id && s.Status != "Resolved"))
            .Select(c => c.Id)
            .Take(20)
            .ToListAsync(cancellationToken);
        if (resolvedClustersNotCompleted.Count > 0)
        {
            errors.Add($"SOS clusters with only resolved requests must be Completed: {string.Join(", ", resolvedClustersNotCompleted)}.");
        }

        var clusteredSosPriorities = await db.SosRequests
            .Where(s => s.ClusterId.HasValue)
            .Select(s => new { ClusterId = s.ClusterId!.Value, s.PriorityLevel })
            .ToListAsync(cancellationToken);
        var oversizedClusters = clusteredSosPriorities
            .GroupBy(s => s.ClusterId)
            .Select(group =>
            {
                var priorities = group.Select(s => s.PriorityLevel).ToList();
                return new
                {
                    ClusterId = group.Key,
                    Count = group.Count(),
                    HighestPriority = HighestSosPriority(priorities),
                    Limit = MaxSosRequestsForCluster(priorities)
                };
            })
            .Where(cluster => cluster.Count > cluster.Limit)
            .Take(20)
            .ToList();
        if (oversizedClusters.Count > 0)
        {
            errors.Add(
                "SOS clusters exceed severity-based request limits: "
                + string.Join(
                    "; ",
                    oversizedClusters.Select(cluster =>
                        $"cluster #{cluster.ClusterId} has {cluster.Count}/{cluster.Limit} SOS requests for {cluster.HighestPriority}"))
                + ".");
        }

        var badSosTypes = await db.SosRequests
            .Where(s => s.SosType != null && !SosTypes.Contains(s.SosType))
            .Select(s => s.SosType!)
            .Distinct()
            .ToListAsync(cancellationToken);
        AddInvalidValues(errors, "sos_requests.sos_type", badSosTypes);

        var badMissionTypes = await db.Missions
            .Where(m => m.MissionType != null && !MissionTypes.Contains(m.MissionType))
            .Select(m => m.MissionType!)
            .Distinct()
            .ToListAsync(cancellationToken);
        AddInvalidValues(errors, "missions.mission_type", badMissionTypes);

        var badMissionStatuses = await db.Missions
            .Where(m => m.Status != null && !MissionStatuses.Contains(m.Status))
            .Select(m => m.Status!)
            .Distinct()
            .ToListAsync(cancellationToken);
        AddInvalidValues(errors, "missions.status", badMissionStatuses);

        var badActivityStatuses = await db.MissionActivities
            .Where(a => a.Status != null && !ActivityStatuses.Contains(a.Status))
            .Select(a => a.Status!)
            .Distinct()
            .ToListAsync(cancellationToken);
        AddInvalidValues(errors, "mission_activities.status", badActivityStatuses);

        var badIncidentStatuses = await db.TeamIncidents
            .Where(i => i.Status != null && !IncidentStatuses.Contains(i.Status))
            .Select(i => i.Status!)
            .Distinct()
            .ToListAsync(cancellationToken);
        AddInvalidValues(errors, "team_incidents.status", badIncidentStatuses);

        var negativeInventories = await db.SupplyInventories
            .CountAsync(i => (i.Quantity ?? 0) < 0
                || i.MissionReservedQuantity < 0
                || i.TransferReservedQuantity < 0
                || i.MissionReservedQuantity + i.TransferReservedQuantity > (i.Quantity ?? 0), cancellationToken);
        if (negativeInventories > 0)
        {
            errors.Add($"Inventory has {negativeInventories} rows with invalid non-negative/reserved quantities.");
        }

        var invalidLots = await db.SupplyInventoryLots
            .CountAsync(l => l.Quantity < 0 || l.RemainingQuantity < 0 || l.RemainingQuantity > l.Quantity, cancellationToken);
        if (invalidLots > 0)
        {
            errors.Add($"Inventory lots have {invalidLots} rows with invalid remaining quantity.");
        }

        var consumableInventories = await db.SupplyInventories
            .Include(i => i.ItemModel)
            .Include(i => i.Lots)
            .Include(i => i.InventoryLogs)
            .Where(i => i.ItemModel != null && i.ItemModel.ItemType == "Consumable")
            .ToListAsync(cancellationToken);

        var inventoriesWithoutInboundHistory = consumableInventories
            .Count(i => !i.InventoryLogs.Any(log =>
                log.ActionType == "Import"
                || log.ActionType == "TransferIn"
                || log.ActionType == "Return"
                || (log.ActionType == "Adjust" && (log.QuantityChange ?? 0) > 0)));
        if (inventoriesWithoutInboundHistory > 0)
        {
            errors.Add($"{inventoriesWithoutInboundHistory} consumable inventories are missing inbound history.");
        }

        var lotBalanceMismatches = consumableInventories
            .Count(i => i.Lots.Sum(lot => lot.RemainingQuantity) != (i.Quantity ?? 0));
        if (lotBalanceMismatches > 0)
        {
            errors.Add($"{lotBalanceMismatches} consumable inventories do not match lot remaining totals.");
        }

        var inventoryLogBalanceMismatches = consumableInventories
            .Count(i => CalculateConsumableBalance(i.InventoryLogs) != (i.Quantity ?? 0));
        if (inventoryLogBalanceMismatches > 0)
        {
            errors.Add($"{inventoryLogBalanceMismatches} consumable inventories do not match inventory log balance.");
        }

        var ambiguousReusableStatusIds = await db.ReusableItems
            .Where(item => item.Status == "Reserved" || item.Status == "InTransit")
            .Select(item => item.Id)
            .Take(20)
            .ToListAsync(cancellationToken);
        if (ambiguousReusableStatusIds.Count > 0)
        {
            errors.Add(
                "Reusable items must not be seeded as Reserved/InTransit without normalized mission or transfer source: "
                + string.Join(", ", ambiguousReusableStatusIds)
                + ".");
        }

        var reusableReturnSourceResult = await FindReusableReturnSourceIdsAsync(db, cancellationToken);
        errors.AddRange(reusableReturnSourceResult.Errors);
        var reusableReturnSourceIds = reusableReturnSourceResult.ReusableItemIds;
        var inUseReusableWithoutReturnSource = await db.ReusableItems
            .Where(item => item.Status == "InUse" && !reusableReturnSourceIds.Contains(item.Id))
            .Select(item => item.Id)
            .Take(20)
            .ToListAsync(cancellationToken);
        if (inUseReusableWithoutReturnSource.Count > 0)
        {
            errors.Add(
                "Reusable items marked InUse must be referenced by a pending RETURN_SUPPLIES expected return source: "
                + string.Join(", ", inUseReusableWithoutReturnSource)
                + ".");
        }

        var returnSourceReusableWithUnexpectedStatus = await db.ReusableItems
            .Where(item => reusableReturnSourceIds.Contains(item.Id) && item.Status != "InUse")
            .Select(item => item.Id)
            .Take(20)
            .ToListAsync(cancellationToken);
        if (returnSourceReusableWithUnexpectedStatus.Count > 0)
        {
            errors.Add(
                "Reusable items referenced by pending RETURN_SUPPLIES expected return source must be marked InUse: "
                + string.Join(", ", returnSourceReusableWithUnexpectedStatus)
                + ".");
        }

        var inventoryLogs = await db.InventoryLogs.ToListAsync(cancellationToken);
        var requiredInventoryActions = new[] { "Import", "Export", "TransferOut", "TransferIn", "Adjust", "Return" };
        var missingActions = requiredInventoryActions
            .Where(action => !inventoryLogs.Any(log => log.ActionType == action))
            .ToList();
        if (missingActions.Count > 0)
        {
            errors.Add($"Inventory logs are missing action types: {string.Join(", ", missingActions)}.");
        }

        var conversationsWithoutVictimParticipant = await db.Conversations
            .Where(c => c.VictimId != null)
            .CountAsync(c => !db.ConversationParticipants.Any(p =>
                p.ConversationId == c.Id && p.UserId == c.VictimId && p.RoleInConversation == "Victim"), cancellationToken);
        if (conversationsWithoutVictimParticipant > 0)
        {
            errors.Add($"{conversationsWithoutVictimParticipant} conversations are missing their victim participant.");
        }

        var duplicateActiveTeamMembers = await db.RescueTeamMembers
            .Where(m => m.Team != null && m.Team.Status != "Disbanded")
            .GroupBy(m => m.UserId)
            .Where(g => g.Count() > 1)
            .CountAsync(cancellationToken);
        if (duplicateActiveTeamMembers > 0)
        {
            errors.Add($"{duplicateActiveTeamMembers} rescuers are assigned to more than one active rescue team.");
        }

        var assignedTeamsWithoutAssignments = await db.RescueTeams
            .CountAsync(team => team.Status == "Assigned"
                && !db.MissionTeams.Any(missionTeam =>
                    missionTeam.RescuerTeamId == team.Id
                    && missionTeam.UnassignedAt == null
                    && missionTeam.Status == "Assigned"), cancellationToken);
        if (assignedTeamsWithoutAssignments > 0)
        {
            errors.Add($"{assignedTeamsWithoutAssignments} rescue teams are marked Assigned without an active assigned mission.");
        }

        var missionTeamsWithoutExecution = await db.RescueTeams
            .CountAsync(team => team.Status == "OnMission"
                && !db.MissionTeams.Any(missionTeam =>
                    missionTeam.RescuerTeamId == team.Id
                    && missionTeam.UnassignedAt == null
                    && missionTeam.Status == "InProgress"), cancellationToken);
        if (missionTeamsWithoutExecution > 0)
        {
            errors.Add($"{missionTeamsWithoutExecution} rescue teams are marked OnMission without an in-progress mission.");
        }

        return errors;
    }

    private static void AddInvalidValues(ICollection<string> errors, string field, IReadOnlyCollection<string> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        errors.Add($"{field} contains invalid values: {string.Join(", ", values)}.");
    }

    private static string HighestSosPriority(IEnumerable<string?> priorities)
    {
        var prioritySet = priorities
            .Where(priority => !string.IsNullOrWhiteSpace(priority))
            .ToHashSet(StringComparer.Ordinal);

        if (prioritySet.Contains("Critical"))
        {
            return "Critical";
        }

        if (prioritySet.Contains("High"))
        {
            return "High";
        }

        if (prioritySet.Contains("Medium"))
        {
            return "Medium";
        }

        return "Low";
    }

    private static int MaxSosRequestsForCluster(IEnumerable<string?> priorities) =>
        HighestSosPriority(priorities) switch
        {
            "Critical" => 1,
            "High" => 2,
            "Medium" => 3,
            _ => 5
        };

    private static int CalculateConsumableBalance(IEnumerable<RESQ.Infrastructure.Entities.Logistics.InventoryLog> logs)
    {
        return logs.Sum(log =>
        {
            var quantity = log.QuantityChange ?? 0;
            return log.ActionType switch
            {
                "Import" or "TransferIn" or "Return" => quantity,
                "Export" or "TransferOut" => -quantity,
                "Adjust" => quantity,
                _ => 0
            };
        });
    }

    private static async Task<(HashSet<int> ReusableItemIds, List<string> Errors)> FindReusableReturnSourceIdsAsync(
        ResQDbContext db,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var reusableItemIds = new HashSet<int>();
        var activities = await db.MissionActivities
            .Where(activity => activity.ActivityType == "RETURN_SUPPLIES"
                && activity.Status == "PendingConfirmation"
                && activity.Items != null)
            .Select(activity => new { activity.Id, activity.Items })
            .ToListAsync(cancellationToken);

        foreach (var activity in activities)
        {
            try
            {
                using var document = JsonDocument.Parse(activity.Items ?? "[]");
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    errors.Add($"RETURN_SUPPLIES activity #{activity.Id} items payload must be a JSON array.");
                    continue;
                }

                foreach (var itemElement in document.RootElement.EnumerateArray())
                {
                    if (!TryGetPropertyIgnoreCase(itemElement, "expectedReturnUnits", out var unitsElement)
                        || unitsElement.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var unitElement in unitsElement.EnumerateArray())
                    {
                        if (TryGetPropertyIgnoreCase(unitElement, "reusableItemId", out var idElement)
                            && idElement.TryGetInt32(out var reusableItemId))
                        {
                            reusableItemIds.Add(reusableItemId);
                        }
                    }
                }
            }
            catch (JsonException)
            {
                errors.Add($"RETURN_SUPPLIES activity #{activity.Id} items payload is not valid JSON.");
            }
        }

        return (reusableItemIds, errors);
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
