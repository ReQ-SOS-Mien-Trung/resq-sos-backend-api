using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services.Logistics;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Logistics;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Persistence.Logistics;

public class DepotInventoryRepository(ResQDbContext context, IInventoryQueryService inventoryQueryService) : IDepotInventoryRepository
{
    private readonly ResQDbContext _context = context;
    private readonly IInventoryQueryService _inventoryQueryService = inventoryQueryService;

    public async Task<int?> GetActiveDepotIdByManagerAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var managerRecord = await _context.DepotManagers
            .AsNoTracking()
            .Where(dm => dm.UserId == userId && dm.UnassignedAt == null)
            .Select(dm => dm.DepotId)
            .FirstOrDefaultAsync(cancellationToken);

        return managerRecord;
    }

    public async Task<PagedResult<InventoryItemModel>> GetInventoryPagedAsync(
        int depotId, 
        List<int>? categoryIds, 
        List<ItemType>? itemTypes, 
        List<TargetGroup>? targetGroups, 
        int pageNumber, 
        int pageSize, 
        CancellationToken cancellationToken = default)
    {
        var query = from inv in _context.DepotSupplyInventories.AsNoTracking()
                    join ri in _context.ReliefItems on inv.ReliefItemId equals ri.Id
                    join cat in _context.ItemCategories on ri.CategoryId equals cat.Id into catGroup
                    from cat in catGroup.DefaultIfEmpty()
                    where inv.DepotId == depotId
                    select new { inv, ri, cat };

        if (categoryIds != null && categoryIds.Count != 0)
        {
            query = query.Where(x => x.cat != null && categoryIds.Contains(x.cat.Id));
        }

        if (itemTypes != null && itemTypes.Count != 0)
        {
            var itemTypeStrings = itemTypes.Select(e => e.ToString()).ToList();
            query = query.Where(x => x.ri.ItemType != null && itemTypeStrings.Contains(x.ri.ItemType));
        }

        if (targetGroups != null && targetGroups.Count != 0)
        {
            var targetGroupStrings = targetGroups.Select(e => e.ToString().ToLower()).ToList();
            query = query.Where(x => x.ri.TargetGroup != null && targetGroupStrings.Contains(x.ri.TargetGroup.ToLower()));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var pagedData = await query
            .OrderByDescending(x => x.inv.LastStockedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = pagedData.Select(x => new InventoryItemModel
        {
            ReliefItemId = x.ri.Id,
            ReliefItemName = x.ri.Name ?? string.Empty,
            CategoryId = x.ri.CategoryId,
            CategoryName = x.cat?.Name ?? string.Empty,
            ItemType = x.ri.ItemType,
            TargetGroup = x.ri.TargetGroup,
            Availability = _inventoryQueryService.ComputeAvailability(x.inv.Quantity, x.inv.ReservedQuantity),
            LastStockedAt = x.inv.LastStockedAt
        }).ToList();

        return new PagedResult<InventoryItemModel>(items, totalCount, pageNumber, pageSize);
    }
}
