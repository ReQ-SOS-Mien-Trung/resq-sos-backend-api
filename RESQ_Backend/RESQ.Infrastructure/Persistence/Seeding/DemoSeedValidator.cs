using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Persistence.Seeding;

public sealed class DemoSeedValidator
{
    private static readonly HashSet<string> Priorities = new(StringComparer.Ordinal)
    {
        "Low", "Medium", "High", "Critical"
    };

    private static readonly HashSet<string> SosStatuses = new(StringComparer.Ordinal)
    {
        "Pending", "Assigned", "InProgress", "Incident", "Resolved", "Cancelled"
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
            .Where(m => m.Team != null && m.Team.Status != "Disbanded" && m.Team.Status != "Unavailable")
            .GroupBy(m => m.UserId)
            .Where(g => g.Count() > 1)
            .CountAsync(cancellationToken);
        if (duplicateActiveTeamMembers > 0)
        {
            errors.Add($"{duplicateActiveTeamMembers} rescuers are assigned to more than one active rescue team.");
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
}
