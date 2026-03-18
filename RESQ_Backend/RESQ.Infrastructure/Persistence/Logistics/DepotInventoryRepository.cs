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

        // No filter when itemTypes is null, empty, or contains ALL possible values (equivalent to no filter).
        var totalItemTypeCount = Enum.GetValues<ItemType>().Length;
        var itemTypeSet = (itemTypes != null && itemTypes.Count > 0)
            ? itemTypes.ToHashSet()
            : null;
        var hasItemTypeFilter = itemTypeSet != null && itemTypeSet.Count < totalItemTypeCount;

        var targetGroupStrings = targetGroups?.Select(e => e.ToString().ToLower()).ToList() ?? new List<string>();
        var hasTargetGroupFilter = targetGroupStrings.Count > 0;

        // Determine which tables to query based on the ItemType filter.
        // No filter (null/empty/all-types) → include both; explicit filter → include only matching types.
        bool includeConsumable = !hasItemTypeFilter || itemTypeSet!.Contains(ItemType.Consumable);
        bool includeReusable   = !hasItemTypeFilter || itemTypeSet!.Contains(ItemType.Reusable);

        var combined = new List<InventoryItemModel>();

        // ── Consumable items from depot_supply_inventory ──────────────────────
        if (includeConsumable)
        {
            var consumableRaw = await (
                from inv in _context.SupplyInventories.AsNoTracking()
                join ri  in _context.ItemModels.AsNoTracking() on inv.ItemModelId equals ri.Id
                where inv.DepotId == depotId
                   && (!hasCategoryFilter  || safeCategoryIds.Contains(ri.CategoryId ?? 0))
                   && (!hasTargetGroupFilter || (ri.TargetGroup != null && targetGroupStrings.Contains(ri.TargetGroup.ToLower())))
                select new
                {
                    ri.Id, ri.Name, ri.CategoryId, ri.ItemType, ri.TargetGroup,
                    Quantity         = inv.Quantity         ?? 0,
                    ReservedQuantity = inv.ReservedQuantity ?? 0,
                    LastStockedAt    = inv.LastStockedAt
                }
            ).ToListAsync(cancellationToken);

            var catIds = consumableRaw.Where(x => x.CategoryId.HasValue).Select(x => x.CategoryId!.Value).Distinct().ToList();
            var catDict = await _context.Categories.AsNoTracking()
                .Where(c => catIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Name ?? string.Empty, cancellationToken);

            combined.AddRange(consumableRaw.Select(x => new InventoryItemModel
            {
                ItemModelId   = x.Id,
                ItemModelName = x.Name ?? string.Empty,
                CategoryId     = x.CategoryId,
                CategoryName   = x.CategoryId.HasValue && catDict.TryGetValue(x.CategoryId.Value, out var cn) ? cn : string.Empty,
                ItemType       = x.ItemType,
                TargetGroup    = x.TargetGroup,
                Availability   = _inventoryQueryService.ComputeAvailability(x.Quantity, x.ReservedQuantity),
                LastStockedAt  = x.LastStockedAt
            }));
        }

        // ── Reusable items from reusable_items (aggregated per relief item) ─────
        if (includeReusable)
        {
            var reusableRaw = await (
                from dri in _context.ReusableItems.AsNoTracking()
                join ri  in _context.ItemModels.AsNoTracking() on dri.ItemModelId equals ri.Id
                where dri.DepotId == depotId
                   && (!hasCategoryFilter  || safeCategoryIds.Contains(ri.CategoryId ?? 0))
                   && (!hasTargetGroupFilter || (ri.TargetGroup != null && targetGroupStrings.Contains(ri.TargetGroup.ToLower())))
                group new { dri, ri } by new { ri.Id, ri.Name, ri.CategoryId, ri.ItemType, ri.TargetGroup } into g
                select new
                {
                    Id                  = g.Key.Id,
                    Name                = g.Key.Name,
                    CategoryId          = g.Key.CategoryId,
                    ItemType            = g.Key.ItemType,
                    TargetGroup         = g.Key.TargetGroup,
                    TotalUnits          = g.Count(),
                    AvailableUnits      = g.Count(x => x.dri.Status == "Available"),
                    InUseUnits          = g.Count(x => x.dri.Status == "InUse"),
                    MaintenanceUnits    = g.Count(x => x.dri.Status == "Maintenance"),
                    DecommissionedUnits = g.Count(x => x.dri.Status == "Decommissioned"),
                    GoodCount           = g.Count(x => x.dri.Condition == "Good"),
                    FairCount           = g.Count(x => x.dri.Condition == "Fair"),
                    PoorCount           = g.Count(x => x.dri.Condition == "Poor"),
                    LastStockedAt       = g.Max(x => x.dri.CreatedAt)
                }
            ).ToListAsync(cancellationToken);

            var catIds = reusableRaw.Where(x => x.CategoryId.HasValue).Select(x => x.CategoryId!.Value).Distinct().ToList();
            var catDict = await _context.Categories.AsNoTracking()
                .Where(c => catIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Name ?? string.Empty, cancellationToken);

            combined.AddRange(reusableRaw.Select(x => new InventoryItemModel
            {
                ItemModelId      = x.Id,
                ItemModelName    = x.Name ?? string.Empty,
                CategoryId        = x.CategoryId,
                CategoryName      = x.CategoryId.HasValue && catDict.TryGetValue(x.CategoryId.Value, out var cn) ? cn : string.Empty,
                ItemType          = x.ItemType,
                TargetGroup       = x.TargetGroup,
                Availability      = _inventoryQueryService.ComputeAvailability(
                                        x.TotalUnits,
                                        x.InUseUnits + x.MaintenanceUnits),
                LastStockedAt     = x.LastStockedAt,
                ReusableBreakdown = new RESQ.Domain.Entities.Logistics.ValueObjects.ReusableBreakdown
                {
                    TotalUnits          = x.TotalUnits,
                    AvailableUnits      = x.AvailableUnits,
                    InUseUnits          = x.InUseUnits,
                    MaintenanceUnits    = x.MaintenanceUnits,
                    DecommissionedUnits = x.DecommissionedUnits,
                    GoodCount           = x.GoodCount,
                    FairCount           = x.FairCount,
                    PoorCount           = x.PoorCount
                }
            }));
        }

        var totalCount = combined.Count;
        var pagedItems = combined
            .OrderByDescending(x => x.LastStockedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<InventoryItemModel>(pagedItems, totalCount, pageNumber, pageSize);
    }

    public async Task<(List<AgentInventoryItem> Items, int TotalCount)> SearchForAgentAsync(
        string categoryKeyword,
        string? typeKeyword,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = from dsi in _context.SupplyInventories.AsNoTracking()
                    join ri in _context.ItemModels.AsNoTracking() on dsi.ItemModelId equals ri.Id
                    join cat in _context.Categories.AsNoTracking() on ri.CategoryId equals cat.Id
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
            from cat in _context.Categories.AsNoTracking()
            orderby cat.Name
            select new DepotCategoryQuantityDto
            {
                CategoryId   = cat.Id,
                CategoryCode = cat.Code ?? string.Empty,
                CategoryName = cat.Name ?? string.Empty,
                // Consumable items: sum of quantities in supply_inventory
                TotalConsumableQuantity = (
                    from dsi in _context.SupplyInventories
                    join ri in _context.ItemModels on dsi.ItemModelId equals ri.Id
                    where dsi.DepotId == depotId && ri.CategoryId == cat.Id
                    select (int?)dsi.Quantity
                ).Sum() ?? 0,
                // Consumable items: available = Quantity - ReservedQuantity
                AvailableConsumableQuantity = (
                    from dsi in _context.SupplyInventories
                    join ri in _context.ItemModels on dsi.ItemModelId equals ri.Id
                    where dsi.DepotId == depotId && ri.CategoryId == cat.Id
                    select (int?)(dsi.Quantity - dsi.ReservedQuantity)
                ).Sum() ?? 0,
                // Reusable items: total physical units in reusable_items
                TotalReusableUnits = (
                    from dri in _context.ReusableItems
                    join ri in _context.ItemModels on dri.ItemModelId equals ri.Id
                    where dri.DepotId == depotId && ri.CategoryId == cat.Id
                    select (int?)dri.Id
                ).Count(),
                AvailableReusableUnits = (
                    from dri in _context.ReusableItems
                    join ri in _context.ItemModels on dri.ItemModelId equals ri.Id
                    where dri.DepotId == depotId && ri.CategoryId == cat.Id
                          && dri.Status == "Available"
                    select (int?)dri.Id
                ).Count()
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
        List<int>? itemModelIds,
        Dictionary<int, int> itemQuantities,
        bool activeDepotsOnly,
        int? excludeDepotId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var safeIds = itemModelIds ?? new List<int>();
        var hasIdFilter = safeIds.Count > 0;

        // ── 1. Consumable rows — tracked by quantity in supply_inventory ──────
        var consumableQuery = from dsi in _context.SupplyInventories.AsNoTracking()
                              join ri    in _context.ItemModels.AsNoTracking()  on dsi.ItemModelId equals ri.Id
                              join cat   in _context.Categories.AsNoTracking()  on ri.CategoryId  equals cat.Id
                              join depot in _context.Depots.AsNoTracking()      on dsi.DepotId    equals depot.Id
                              select new { dsi, ri, cat, depot };

        if (excludeDepotId.HasValue)
            consumableQuery = consumableQuery.Where(x => x.depot.Id != excludeDepotId.Value);
        if (activeDepotsOnly)
            consumableQuery = consumableQuery.Where(x => x.depot.Status == "Available" || x.depot.Status == "Full");
        if (hasIdFilter)
            consumableQuery = consumableQuery.Where(x => safeIds.Contains(x.ri.Id));
        consumableQuery = consumableQuery.Where(x => (x.dsi.Quantity ?? 0) - (x.dsi.ReservedQuantity ?? 0) >= 1);

        // Load consumable rows; geometry coords extracted in-memory to avoid ST_Y in SQL
        var consumableRaw = await consumableQuery
            .Select(x => new
            {
                ItemModelId       = x.ri.Id,
                ItemModelName     = x.ri.Name ?? string.Empty,
                CategoryName      = x.cat.Name ?? string.Empty,
                ItemType          = x.ri.ItemType,
                Unit              = x.ri.Unit,
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

        // ── 2. Reusable rows — each physical unit tracked individually ────────
        var reusableQuery = from dri in _context.ReusableItems.AsNoTracking()
                            join ri    in _context.ItemModels.AsNoTracking()  on dri.ItemModelId equals ri.Id
                            join cat   in _context.Categories.AsNoTracking()  on ri.CategoryId  equals cat.Id
                            join depot in _context.Depots.AsNoTracking()      on dri.DepotId    equals depot.Id
                            select new { dri, ri, cat, depot };

        if (excludeDepotId.HasValue)
            reusableQuery = reusableQuery.Where(x => x.depot.Id != excludeDepotId.Value);
        if (activeDepotsOnly)
            reusableQuery = reusableQuery.Where(x => x.depot.Status == "Available" || x.depot.Status == "Full");
        if (hasIdFilter)
            reusableQuery = reusableQuery.Where(x => safeIds.Contains(x.ri.Id));

        // Load individual unit rows (one row per physical unit)
        var reusableUnits = await reusableQuery
            .Select(x => new
            {
                ItemModelId   = x.ri.Id,
                ItemModelName = x.ri.Name ?? string.Empty,
                CategoryName  = x.cat.Name ?? string.Empty,
                ItemType      = x.ri.ItemType,
                Unit          = x.ri.Unit,
                DepotId       = x.depot.Id,
                DepotName     = x.depot.Name ?? string.Empty,
                DepotAddress  = x.depot.Address ?? string.Empty,
                DepotStatus   = x.depot.Status,
                DepotLocation = x.depot.Location,
                IsAvailable   = x.dri.Status == "Available",
                CreatedAt     = x.dri.CreatedAt
            })
            .ToListAsync(cancellationToken);

        // Aggregate individual units → one row per (ItemModelId, DepotId) in memory
        var reusableRaw = reusableUnits
            .GroupBy(x => new { x.ItemModelId, x.DepotId })
            .Where(g => g.Any(x => x.IsAvailable)) // only depots that have ≥ 1 available unit
            .Select(g =>
            {
                var f         = g.First();
                var total     = g.Count();
                var available = g.Count(x => x.IsAvailable);
                return new
                {
                    ItemModelId       = f.ItemModelId,
                    ItemModelName     = f.ItemModelName,
                    CategoryName      = f.CategoryName,
                    ItemType          = f.ItemType,
                    Unit              = f.Unit,
                    DepotId           = f.DepotId,
                    DepotName         = f.DepotName,
                    DepotAddress      = f.DepotAddress,
                    DepotStatus       = f.DepotStatus,
                    DepotLocation     = f.DepotLocation,
                    TotalQuantity     = total,
                    ReservedQuantity  = total - available,
                    AvailableQuantity = available,
                    LastStockedAt     = g.Max(x => x.CreatedAt)
                };
            })
            .ToList();

        // ── 3. Merge consumable + reusable into a single candidate list ───────
        // Both projections share the same anonymous-type shape so Concat is type-safe
        var rawRows = consumableRaw.Concat(reusableRaw).ToList();

        // Apply per-item minimum-quantity filter in memory
        var filtered = rawRows
            .Where(r =>
            {
                var minQty = itemQuantities.TryGetValue(r.ItemModelId, out var q) ? q : 1;
                return r.AvailableQuantity >= minQty;
            })
            .ToList();

        // Paginate by distinct item
        var totalItemCount = filtered.Select(r => r.ItemModelId).Distinct().Count();

        if (totalItemCount == 0)
            return (new List<WarehouseItemRow>(), 0);

        var pagedItemIds = filtered
            .Select(r => new { r.ItemModelId, r.ItemModelName })
            .DistinctBy(r => r.ItemModelId)
            .OrderBy(r => r.ItemModelName)
            .ThenBy(r => r.ItemModelId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(r => r.ItemModelId)
            .ToHashSet();

        var rows = filtered
            .Where(r => pagedItemIds.Contains(r.ItemModelId))
            .Select(x => new WarehouseItemRow
            {
                ItemModelId       = x.ItemModelId,
                ItemModelName     = x.ItemModelName,
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

    public async Task<List<SupplyShortageResult>> CheckSupplyAvailabilityAsync(
        int depotId,
        List<(int ItemModelId, string ItemName, int RequestedQuantity)> items,
        CancellationToken cancellationToken = default)
    {
        var itemModelIds = items.Select(i => i.ItemModelId).ToList();

        var inventory = await _context.SupplyInventories
            .AsNoTracking()
            .Where(inv => inv.DepotId == depotId && inv.ItemModelId != null && itemModelIds.Contains(inv.ItemModelId!.Value))
            .Select(inv => new
            {
                ItemModelId = inv.ItemModelId!.Value,
                Available = (inv.Quantity ?? 0) - (inv.ReservedQuantity ?? 0)
            })
            .ToDictionaryAsync(x => x.ItemModelId, x => x.Available, cancellationToken);

        var shortages = new List<SupplyShortageResult>();
        foreach (var (itemModelId, itemName, requestedQty) in items)
        {
            if (!inventory.TryGetValue(itemModelId, out var available))
            {
                shortages.Add(new SupplyShortageResult
                {
                    ItemModelId = itemModelId,
                    ItemName = itemName,
                    RequestedQuantity = requestedQty,
                    AvailableQuantity = 0,
                    NotFound = true
                });
            }
            else if (available < requestedQty)
            {
                shortages.Add(new SupplyShortageResult
                {
                    ItemModelId = itemModelId,
                    ItemName = itemName,
                    RequestedQuantity = requestedQty,
                    AvailableQuantity = available,
                    NotFound = false
                });
            }
        }

        return shortages;
    }
}
