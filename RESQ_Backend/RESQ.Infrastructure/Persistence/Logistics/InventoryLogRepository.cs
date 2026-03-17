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
        int? itemModelId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.InventoryLogs
            .Include(x => x.SupplyInventory)
                .ThenInclude(x => x!.Depot)
            .Include(x => x.SupplyInventory)
                .ThenInclude(x => x!.ItemModel)
            .Include(x => x.PerformedByUser)
            .AsQueryable();

        // Filter by depot if specified
        if (depotId.HasValue)
        {
            query = query.Where(x => x.SupplyInventory!.DepotId == depotId.Value);
        }

        // Filter by item model if specified
        if (itemModelId.HasValue)
        {
            query = query.Where(x => x.SupplyInventory!.ItemModelId == itemModelId.Value);
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
                DepotId = x.SupplyInventory!.DepotId,
                DepotName = x.SupplyInventory!.Depot != null ? x.SupplyInventory.Depot.Name : string.Empty,
                ItemModelId = x.SupplyInventory!.ItemModelId,
                ItemModelName = x.SupplyInventory!.ItemModel != null ? x.SupplyInventory.ItemModel.Name : string.Empty
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
            .Include(x => x.SupplyInventory)
                .ThenInclude(x => x!.Depot)
            .Include(x => x.SupplyInventory)
                .ThenInclude(x => x!.ItemModel)
                    .ThenInclude(x => x!.Category)
            .Include(x => x.PerformedByUser)
            .AsQueryable();

        // Filters
        if (depotId.HasValue)
        {
            query = query.Where(x => x.SupplyInventory!.DepotId == depotId.Value);
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
                    ItemId = item.SupplyInventory?.ItemModelId ?? 0,
                    ItemName = item.SupplyInventory?.ItemModel?.Name ?? string.Empty,
                    QuantityChange = item.QuantityChange ?? 0,
                    FormattedQuantityChange = FormatQuantityChange(item.ActionType ?? string.Empty, item.QuantityChange ?? 0),
                    Unit = item.SupplyInventory?.ItemModel?.Unit ?? string.Empty,
                    ItemType = item.SupplyInventory?.ItemModel?.ItemType ?? string.Empty,
                    TargetGroup = item.SupplyInventory?.ItemModel?.TargetGroup ?? string.Empty,
                    CategoryName = item.SupplyInventory?.ItemModel?.Category?.Name
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