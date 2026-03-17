using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryByCategory;
using RESQ.Application.UseCases.Logistics.Queries.SearchWarehousesByItems;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Entities.Logistics.Services;
using RESQ.Domain.Enum.Logistics;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Persistence.Logistics;

public class DepotInventoryRepository(IUnitOfWork unitOfWork, IInventoryQueryService inventoryQueryService, ResQDbContext context) : IDepotInventoryRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IInventoryQueryService _inventoryQueryService = inventoryQueryService;
    private readonly ResQDbContext _context = context;

    public async Task<int?> GetActiveDepotIdByManagerAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var managerRecord = await _unitOfWork.GetRepository<DepotManager>()
            .GetByPropertyAsync(
                dm => dm.UserId == userId && dm.UnassignedAt == null, 
                tracked: false
            );

        return managerRecord?.DepotId;
    }

    public async Task<List<int>> GetActiveDepotIdsByManagerAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var records = await _unitOfWork.GetRepository<DepotManager>()
            .GetAllByPropertyAsync(dm => dm.UserId == userId && dm.UnassignedAt == null);

        return records.Where(dm => dm.DepotId.HasValue).Select(dm => dm.DepotId!.Value).ToList();
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
        var safeCategoryIds = categoryIds ?? new List<int>();
        var hasCategoryFilter = safeCategoryIds.Count > 0;

        var itemTypeStrings = itemTypes?.Select(e => e.ToString()).ToList() ?? new List<string>();
        var hasItemTypeFilter = itemTypeStrings.Count > 0;

        var targetGroupStrings = targetGroups?.Select(e => e.ToString().ToLower()).ToList() ?? new List<string>();
        var hasTargetGroupFilter = targetGroupStrings.Count > 0;

        Expression<Func<DepotSupplyInventory, bool>> filter = inv => 
            inv.DepotId == depotId &&
            (!hasCategoryFilter || (inv.ReliefItem != null && safeCategoryIds.Contains(inv.ReliefItem.CategoryId ?? 0))) &&
            (!hasItemTypeFilter || (inv.ReliefItem != null && inv.ReliefItem.ItemType != null && itemTypeStrings.Contains(inv.ReliefItem.ItemType))) &&
            (!hasTargetGroupFilter || (inv.ReliefItem != null && inv.ReliefItem.TargetGroup != null && targetGroupStrings.Contains(inv.ReliefItem.TargetGroup.ToLower())));

        var pagedEntities = await _unitOfWork.GetRepository<DepotSupplyInventory>().GetPagedAsync(
            pageNumber,
            pageSize,
            filter: filter,
            orderBy: q => q.OrderByDescending(x => x.LastStockedAt),
            includeProperties: "ReliefItem"
        );

        var categoryIdsToFetch = pagedEntities.Items
            .Where(x => x.ReliefItem != null && x.ReliefItem.CategoryId.HasValue)
            .Select(x => x.ReliefItem!.CategoryId!.Value)
            .Distinct()
            .ToList();

        var categories = await _unitOfWork.GetRepository<ItemCategory>()
            .GetAllByPropertyAsync(c => categoryIdsToFetch.Contains(c.Id));

        var categoryDict = categories.ToDictionary(c => c.Id, c => c.Name);

        var items = pagedEntities.Items.Select(x => new InventoryItemModel
        {
            ReliefItemId = x.ReliefItemId ?? 0, 
            ReliefItemName = x.ReliefItem?.Name ?? string.Empty,
            CategoryId = x.ReliefItem?.CategoryId ?? 0,
            CategoryName = (x.ReliefItem?.CategoryId.HasValue == true && categoryDict.TryGetValue(x.ReliefItem.CategoryId.Value, out var catName)) ? (catName ?? string.Empty) : string.Empty,
            ItemType = x.ReliefItem?.ItemType,
            TargetGroup = x.ReliefItem?.TargetGroup,
            Availability = _inventoryQueryService.ComputeAvailability(x.Quantity, x.ReservedQuantity),
            LastStockedAt = x.LastStockedAt
        }).ToList();

        return new PagedResult<InventoryItemModel>(
            items, 
            pagedEntities.TotalCount, 
            pagedEntities.PageNumber, 
            pagedEntities.PageSize
        );
    }

    public async Task<(List<AgentInventoryItem> Items, int TotalCount)> SearchForAgentAsync(
        string categoryKeyword,
        string? typeKeyword,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = from dsi in _context.DepotSupplyInventories.AsNoTracking()
                    join ri in _context.ReliefItems.AsNoTracking() on dsi.ReliefItemId equals ri.Id
                    join cat in _context.ItemCategories.AsNoTracking() on ri.CategoryId equals cat.Id
                    join depot in _context.Depots.AsNoTracking() on dsi.DepotId equals depot.Id
                    where (depot.Status == "Available" || depot.Status == "Full")
                       && (dsi.Quantity ?? 0) - (dsi.ReservedQuantity ?? 0) > 0
                       && EF.Functions.ILike(cat.Name ?? string.Empty, "%" + categoryKeyword + "%")
                    select new { dsi, ri, cat, depot };

        if (!string.IsNullOrWhiteSpace(typeKeyword))
            query = query.Where(x => EF.Functions.ILike(x.ri.ItemType ?? string.Empty, "%" + typeKeyword + "%"));

        var total = await query.CountAsync(ct);

        var rawItems = await query
            .OrderByDescending(x => (x.dsi.Quantity ?? 0) - (x.dsi.ReservedQuantity ?? 0))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                ItemId           = x.ri.Id,
                ItemName         = x.ri.Name ?? string.Empty,
                CategoryName     = x.cat.Name ?? string.Empty,
                x.ri.ItemType,
                x.ri.Unit,
                AvailableQuantity = (x.dsi.Quantity ?? 0) - (x.dsi.ReservedQuantity ?? 0),
                DepotId          = x.depot.Id,
                DepotName        = x.depot.Name ?? string.Empty,
                DepotAddress     = x.depot.Address,
                DepotLocation    = x.depot.Location
            })
            .ToListAsync(ct);

        var items = rawItems.Select(x => new AgentInventoryItem
        {
            ItemId            = x.ItemId,
            ItemName          = x.ItemName,
            CategoryName      = x.CategoryName,
            ItemType          = x.ItemType,
            Unit              = x.Unit,
            AvailableQuantity = x.AvailableQuantity,
            DepotId           = x.DepotId,
            DepotName         = x.DepotName,
            DepotAddress      = x.DepotAddress,
            DepotLatitude     = x.DepotLocation?.Y,
            DepotLongitude    = x.DepotLocation?.X
        }).ToList();

        return (items, total);
    }

    public async Task<List<DepotCategoryQuantityDto>> GetInventoryByCategoryAsync(int depotId, CancellationToken cancellationToken = default)
    {
        var result = await (
            from cat in _context.ItemCategories.AsNoTracking()
            orderby cat.Name
            select new DepotCategoryQuantityDto
            {
                CategoryId = cat.Id,
                CategoryCode = cat.Code ?? string.Empty,
                CategoryName = cat.Name ?? string.Empty,
                TotalQuantity = (
                    from dsi in _context.DepotSupplyInventories
                    join ri in _context.ReliefItems on dsi.ReliefItemId equals ri.Id
                    where dsi.DepotId == depotId && ri.CategoryId == cat.Id
                    select (int?)dsi.Quantity
                ).Sum() ?? 0
            }
        ).ToListAsync(cancellationToken);

        return result;
    }

    public async Task<(double Latitude, double Longitude)?> GetDepotLocationAsync(
        int depotId,
        CancellationToken cancellationToken = default)
    {
        var location = await _context.Depots.AsNoTracking()
            .Where(d => d.Id == depotId)
            .Select(d => d.Location)
            .FirstOrDefaultAsync(cancellationToken);

        if (location == null) return null;
        return (location.Y, location.X);
    }

    public async Task<(List<WarehouseItemRow> Rows, int TotalItemCount)> SearchWarehousesByItemsAsync(
        List<int>? reliefItemIds,
        Dictionary<int, int> itemQuantities,
        bool activeDepotsOnly,
        int? excludeDepotId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var safeIds = reliefItemIds ?? new List<int>();
        var hasIdFilter = safeIds.Count > 0;

        // Base query: join inventory → relief item → category → depot
        var baseQuery = from dsi in _context.DepotSupplyInventories.AsNoTracking()
                        join ri   in _context.ReliefItems.AsNoTracking()    on dsi.ReliefItemId equals ri.Id
                        join cat  in _context.ItemCategories.AsNoTracking() on ri.CategoryId equals cat.Id
                        join depot in _context.Depots.AsNoTracking()        on dsi.DepotId equals depot.Id
                        select new { dsi, ri, cat, depot };

        // Exclude the requesting manager's own depot
        if (excludeDepotId.HasValue)
            baseQuery = baseQuery.Where(x => x.depot.Id != excludeDepotId.Value);

        // Only include depots that are operationally active
        if (activeDepotsOnly)
            baseQuery = baseQuery.Where(x => x.depot.Status == "Available" || x.depot.Status == "Full");

        // Filter by item IDs and ensure at least 1 unit available (SQL pre-filter)
        if (hasIdFilter)
            baseQuery = baseQuery.Where(x => safeIds.Contains(x.ri.Id));

        baseQuery = baseQuery.Where(x => (x.dsi.Quantity ?? 0) - (x.dsi.ReservedQuantity ?? 0) >= 1);

        // Fetch all candidate rows — Step 1 (location coords loaded in-memory to avoid ST_Y on geography)
        var rawRows = await baseQuery
            .OrderBy(x => x.ri.Name)
            .ThenBy(x => x.ri.Id)
            .ThenByDescending(x => (x.dsi.Quantity ?? 0) - (x.dsi.ReservedQuantity ?? 0))
            .Select(x => new
            {
                ReliefItemId      = x.ri.Id,
                ReliefItemName    = x.ri.Name ?? string.Empty,
                CategoryName      = x.cat.Name ?? string.Empty,
                x.ri.ItemType,
                x.ri.Unit,
                DepotId           = x.depot.Id,
                DepotName         = x.depot.Name ?? string.Empty,
                DepotAddress      = x.depot.Address ?? string.Empty,
                DepotStatus       = x.depot.Status,
                DepotLocation     = x.depot.Location,
                TotalQuantity     = x.dsi.Quantity ?? 0,
                ReservedQuantity  = x.dsi.ReservedQuantity ?? 0,
                AvailableQuantity = (x.dsi.Quantity ?? 0) - (x.dsi.ReservedQuantity ?? 0),
                LastStockedAt     = x.dsi.LastStockedAt
            })
            .ToListAsync(cancellationToken);

        // Apply per-item quantity filter in memory
        var filtered = rawRows
            .Where(r =>
            {
                var minQty = itemQuantities.TryGetValue(r.ReliefItemId, out var q) ? q : 1;
                return r.AvailableQuantity >= minQty;
            })
            .ToList();

        // Paginate distinct items
        var totalItemCount = filtered.Select(r => r.ReliefItemId).Distinct().Count();

        if (totalItemCount == 0)
            return (new List<WarehouseItemRow>(), 0);

        var pagedItemIds = filtered
            .Select(r => new { r.ReliefItemId, r.ReliefItemName })
            .DistinctBy(r => r.ReliefItemId)
            .OrderBy(r => r.ReliefItemName)
            .ThenBy(r => r.ReliefItemId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(r => r.ReliefItemId)
            .ToHashSet();

        var rows = filtered
            .Where(r => pagedItemIds.Contains(r.ReliefItemId))
            .Select(x => new WarehouseItemRow
            {
                ReliefItemId      = x.ReliefItemId,
                ReliefItemName    = x.ReliefItemName,
                CategoryName      = x.CategoryName,
                ItemType          = x.ItemType,
                Unit              = x.Unit,
                DepotId           = x.DepotId,
                DepotName         = x.DepotName,
                DepotAddress      = x.DepotAddress,
                DepotStatus       = x.DepotStatus,
                DepotLatitude     = x.DepotLocation?.Y,
                DepotLongitude    = x.DepotLocation?.X,
                TotalQuantity     = x.TotalQuantity,
                ReservedQuantity  = x.ReservedQuantity,
                AvailableQuantity = x.AvailableQuantity,
                LastStockedAt     = x.LastStockedAt
            })
            .ToList();

        return (rows, totalItemCount);
    }
}
