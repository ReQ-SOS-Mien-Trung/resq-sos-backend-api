using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.UseCases.Logistics.Queries.GetInventoryTransactionHistory;
using RESQ.Domain.Entities.Logistics.Models;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Persistence.Logistics;

public class InventoryLogRepository(ResQDbContext context) : IInventoryLogRepository
{
    private readonly ResQDbContext _context = context;

    public async Task<PagedResult<InventoryLogModel>> GetInventoryLogsPagedAsync(
        int? depotId,
        int? reliefItemId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.InventoryLogs
            .Include(x => x.DepotSupplyInventory)
                .ThenInclude(x => x!.Depot)
            .Include(x => x.DepotSupplyInventory)
                .ThenInclude(x => x!.ReliefItem)
            .Include(x => x.PerformedByUser)
            .AsQueryable();

        // Filter by depot if specified
        if (depotId.HasValue)
        {
            query = query.Where(x => x.DepotSupplyInventory!.DepotId == depotId.Value);
        }

        // Filter by relief item if specified
        if (reliefItemId.HasValue)
        {
            query = query.Where(x => x.DepotSupplyInventory!.ReliefItemId == reliefItemId.Value);
        }

        // Order by most recent first
        query = query.OrderByDescending(x => x.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        
        var logs = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new InventoryLogModel
            {
                Id = x.Id,
                DepotSupplyInventoryId = x.DepotSupplyInventoryId,
                ActionType = x.ActionType ?? string.Empty,
                QuantityChange = x.QuantityChange,
                SourceType = x.SourceType ?? string.Empty,
                SourceId = x.SourceId,
                Note = x.Note,
                CreatedAt = x.CreatedAt,
                PerformedByName = x.PerformedByUser != null 
                    ? $"{x.PerformedByUser.LastName} {x.PerformedByUser.FirstName}".Trim() 
                    : string.Empty,
                DepotId = x.DepotSupplyInventory!.DepotId,
                DepotName = x.DepotSupplyInventory!.Depot != null ? x.DepotSupplyInventory.Depot.Name : string.Empty,
                ReliefItemId = x.DepotSupplyInventory!.ReliefItemId,
                ReliefItemName = x.DepotSupplyInventory!.ReliefItem != null ? x.DepotSupplyInventory.ReliefItem.Name : string.Empty
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<InventoryLogModel>(logs, totalCount, pageNumber, pageSize);
    }

    public async Task<PagedResult<InventoryTransactionDto>> GetTransactionHistoryAsync(
        int? depotId,
        List<string>? actionTypes,
        List<string>? sourceTypes,
        DateTime? fromDate,
        DateTime? toDate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.InventoryLogs
            .Include(x => x.DepotSupplyInventory)
                .ThenInclude(x => x!.Depot)
            .Include(x => x.DepotSupplyInventory)
                .ThenInclude(x => x!.ReliefItem)
                    .ThenInclude(x => x!.ItemCategory)
            .Include(x => x.PerformedByUser)
            .AsQueryable();

        // Filters
        if (depotId.HasValue)
        {
            query = query.Where(x => x.DepotSupplyInventory!.DepotId == depotId.Value);
        }

        if (actionTypes != null && actionTypes.Count > 0)
        {
            query = query.Where(x => actionTypes.Contains(x.ActionType!));
        }

        if (sourceTypes != null && sourceTypes.Count > 0)
        {
            query = query.Where(x => sourceTypes.Contains(x.SourceType!));
        }

        if (fromDate.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= toDate.Value);
        }

        // Group by transaction criteria and order by most recent
        var groupedQuery = query
            .GroupBy(x => new { 
                x.CreatedAt!.Value.Date, 
                x.ActionType, 
                x.SourceType, 
                x.SourceId, 
                x.PerformedBy 
            })
            .Select(g => new
            {
                g.Key.Date,
                g.Key.ActionType,
                g.Key.SourceType,
                g.Key.SourceId,
                g.Key.PerformedBy,
                CreatedAt = g.Min(x => x.CreatedAt),
                Items = g.ToList()
            })
            .OrderByDescending(x => x.CreatedAt);

        var totalCount = await groupedQuery.CountAsync(cancellationToken);

        var transactions = await groupedQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var result = transactions.Select(t =>
        {
            var firstItem = t.Items.First();
            var transactionId = $"{t.ActionType}_{t.SourceType}_{t.SourceId}_{t.CreatedAt:yyyyMMdd_HHmmss}";

            return new InventoryTransactionDto
            {
                TransactionId = transactionId,
                ActionType = firstItem.ActionType ?? string.Empty,
                SourceType = firstItem.SourceType ?? string.Empty,
                SourceId = firstItem.SourceId,
                SourceName = GetSourceName(firstItem.SourceType, firstItem.SourceId),
                PerformedByName = firstItem.PerformedByUser != null 
                    ? $"{firstItem.PerformedByUser.FirstName} {firstItem.PerformedByUser.LastName}".Trim() 
                    : string.Empty,
                Note = firstItem.Note,
                CreatedAt = firstItem.CreatedAt ?? DateTime.MinValue,
                Items = t.Items.Select(item => new InventoryTransactionItemDto
                {
                    ItemId = item.DepotSupplyInventory?.ReliefItemId ?? 0,
                    ItemName = item.DepotSupplyInventory?.ReliefItem?.Name ?? string.Empty,
                    QuantityChange = item.QuantityChange ?? 0,
                    FormattedQuantityChange = FormatQuantityChange(item.ActionType ?? string.Empty, item.QuantityChange ?? 0),
                    Unit = item.DepotSupplyInventory?.ReliefItem?.Unit ?? string.Empty,
                    ItemType = item.DepotSupplyInventory?.ReliefItem?.ItemType ?? string.Empty,
                    TargetGroup = item.DepotSupplyInventory?.ReliefItem?.TargetGroup ?? string.Empty,
                    CategoryName = item.DepotSupplyInventory?.ReliefItem?.ItemCategory?.Name
                }).ToList()
            };
        }).ToList();

        return new PagedResult<InventoryTransactionDto>(result, totalCount, pageNumber, pageSize);
    }

    private string GetSourceName(string? sourceType, int? sourceId)
    {
        if (string.IsNullOrEmpty(sourceType) || !sourceId.HasValue)
            return string.Empty;

        return sourceType.ToLower() switch
        {
            "donation" => GetOrganizationName(sourceId.Value),
            "mission" => $"Mission #{sourceId.Value}",
            "transfer" => $"Transfer #{sourceId.Value}",
            "adjustment" => "Manual Adjustment",
            _ => $"{sourceType} #{sourceId.Value}"
        };
    }

    private string GetOrganizationName(int organizationId)
    {
        var organization = _context.Organizations
            .Where(o => o.Id == organizationId)
            .Select(o => o.Name)
            .FirstOrDefault();

        return organization ?? $"Organization #{organizationId}";
    }

    private static string FormatQuantityChange(string actionType, int quantityChange)
    {
        return actionType.ToLower() switch
        {
            "import" or "transferin" or "return" => $"+ {quantityChange}",
            "export" or "transferout" => $"- {Math.Abs(quantityChange)}",
            "adjust" => quantityChange >= 0 ? $"+ {quantityChange}" : $"- {Math.Abs(quantityChange)}",
            _ => quantityChange.ToString()
        };
    }
}