using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryByCategory;
using RESQ.Application.UseCases.Logistics.Queries.SearchWarehousesByItems;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Entities.Logistics.Models;
using RESQ.Domain.Entities.Logistics.Services;
using RESQ.Domain.Enum.Logistics;
using RESQ.Infrastructure.Entities.Logistics;

namespace RESQ.Infrastructure.Persistence.Logistics;

public class DepotInventoryRepository(IUnitOfWork unitOfWork, IInventoryQueryService inventoryQueryService) : IDepotInventoryRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IInventoryQueryService _inventoryQueryService = inventoryQueryService;

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
                from inv in _unitOfWork.GetRepository<SupplyInventory>().AsQueryable()
                join ri  in _unitOfWork.GetRepository<ItemModel>().AsQueryable() on inv.ItemModelId equals ri.Id
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
            var catDict = await _unitOfWork.GetRepository<Category>().AsQueryable()
                .Where(c => catIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Name ?? string.Empty, cancellationToken);

            // Lot summary per item model (FEFO info)
            var lotSummaries = await (
                from lot in _unitOfWork.GetRepository<SupplyInventoryLot>().AsQueryable()
                join inv in _unitOfWork.GetRepository<SupplyInventory>().AsQueryable() on lot.SupplyInventoryId equals inv.Id
                where inv.DepotId == depotId && lot.RemainingQuantity > 0
                group lot by inv.ItemModelId into g
                select new
                {
                    ItemModelId = g.Key,
                    LotCount = g.Count(),
                    NearestExpiryDate = g.Where(l => l.ExpiredDate != null).Min(l => (DateTime?)l.ExpiredDate)
                }
            ).ToListAsync(cancellationToken);
            var lotSummaryDict = lotSummaries.ToDictionary(x => x.ItemModelId ?? 0);

            combined.AddRange(consumableRaw.Select(x =>
            {
                lotSummaryDict.TryGetValue(x.Id, out var ls);
                return new InventoryItemModel
                {
                    ItemModelId    = x.Id,
                    ItemModelName  = x.Name ?? string.Empty,
                    CategoryId     = x.CategoryId,
                    CategoryName   = x.CategoryId.HasValue && catDict.TryGetValue(x.CategoryId.Value, out var cn) ? cn : string.Empty,
                    ItemType       = x.ItemType,
                    TargetGroup    = x.TargetGroup,
                    Availability   = _inventoryQueryService.ComputeAvailability(x.Quantity, x.ReservedQuantity),
                    LastStockedAt  = x.LastStockedAt,
                    LotCount       = ls?.LotCount ?? 0,
                    NearestExpiryDate = ls?.NearestExpiryDate
                };
            }));
        }

        // ── Reusable items from reusable_items (aggregated per relief item) ─────
        if (includeReusable)
        {
            var reusableRaw = await (
                from dri in _unitOfWork.GetRepository<ReusableItem>().AsQueryable()
                join ri  in _unitOfWork.GetRepository<ItemModel>().AsQueryable() on dri.ItemModelId equals ri.Id
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
                    ReservedUnits       = g.Count(x => x.dri.Status == "Reserved"),
                    InTransitUnits      = g.Count(x => x.dri.Status == "InTransit"),
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
            var catDict = await _unitOfWork.GetRepository<Category>().AsQueryable()
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
                                        x.ReservedUnits + x.InTransitUnits + x.InUseUnits + x.MaintenanceUnits),
                LastStockedAt     = x.LastStockedAt,
                ReusableBreakdown = new RESQ.Domain.Entities.Logistics.ValueObjects.ReusableBreakdown
                {
                    TotalUnits          = x.TotalUnits,
                    AvailableUnits      = x.AvailableUnits,
                    ReservedUnits       = x.ReservedUnits,
                    InTransitUnits      = x.InTransitUnits,
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

    public async Task<PagedResult<InventoryLotModel>> GetInventoryLotsAsync(
        int depotId, int itemModelId, int pageNumber, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = from lot in _unitOfWork.GetRepository<SupplyInventoryLot>().AsQueryable()
                    join inv in _unitOfWork.GetRepository<SupplyInventory>().AsQueryable() on lot.SupplyInventoryId equals inv.Id
                    where inv.DepotId == depotId && inv.ItemModelId == itemModelId && lot.RemainingQuantity > 0
                    select lot;

        var totalCount = await query.CountAsync(cancellationToken);

        var lots = await query
            .OrderBy(l => l.ExpiredDate == null ? 1 : 0)  // items WITH expiry first
            .ThenBy(l => l.ExpiredDate)                    // soonest expiry first (FEFO)
            .ThenBy(l => l.ReceivedDate)                   // oldest received first (FIFO tie-breaker)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new InventoryLotModel
            {
                Id = l.Id,
                SupplyInventoryId = l.SupplyInventoryId,
                Quantity = l.Quantity,
                RemainingQuantity = l.RemainingQuantity,
                ReceivedDate = l.ReceivedDate,
                ExpiredDate = l.ExpiredDate,
                SourceType = l.SourceType,
                SourceId = l.SourceId,
                CreatedAt = l.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<InventoryLotModel>(lots, totalCount, pageNumber, pageSize);
    }

    public async Task<(List<AgentInventoryItem> Items, int TotalCount)> SearchForAgentAsync(
        string categoryKeyword,
        string? typeKeyword,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = from dsi in _unitOfWork.GetRepository<SupplyInventory>().AsQueryable()
                    join ri in _unitOfWork.GetRepository<ItemModel>().AsQueryable() on dsi.ItemModelId equals ri.Id
                    join cat in _unitOfWork.GetRepository<Category>().AsQueryable() on ri.CategoryId equals cat.Id
                    join depot in _unitOfWork.GetRepository<Depot>().AsQueryable() on dsi.DepotId equals depot.Id
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
        var categories = _unitOfWork.GetRepository<Category>().AsQueryable();
        var supplyInventories = _unitOfWork.GetRepository<SupplyInventory>().AsQueryable();
        var itemModels = _unitOfWork.GetRepository<ItemModel>().AsQueryable();
        var reusableItems = _unitOfWork.GetRepository<ReusableItem>().AsQueryable();

        var result = await (
            from cat in categories
            orderby cat.Name
            select new DepotCategoryQuantityDto
            {
                CategoryId   = cat.Id,
                CategoryCode = cat.Code ?? string.Empty,
                CategoryName = cat.Name ?? string.Empty,
                // Consumable items: sum of quantities in supply_inventory
                TotalConsumableQuantity = (
                    from dsi in supplyInventories
                    join ri in itemModels on dsi.ItemModelId equals ri.Id
                    where dsi.DepotId == depotId && ri.CategoryId == cat.Id
                    select (int?)dsi.Quantity
                ).Sum() ?? 0,
                // Consumable items: available = Quantity - ReservedQuantity
                AvailableConsumableQuantity = (
                    from dsi in supplyInventories
                    join ri in itemModels on dsi.ItemModelId equals ri.Id
                    where dsi.DepotId == depotId && ri.CategoryId == cat.Id
                    select (int?)(dsi.Quantity - dsi.ReservedQuantity)
                ).Sum() ?? 0,
                // Reusable items: total physical units in reusable_items
                TotalReusableUnits = (
                    from dri in reusableItems
                    join ri in itemModels on dri.ItemModelId equals ri.Id
                    where dri.DepotId == depotId && ri.CategoryId == cat.Id
                    select (int?)dri.Id
                ).Count(),
                AvailableReusableUnits = (
                    from dri in reusableItems
                    join ri in itemModels on dri.ItemModelId equals ri.Id
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
        var location = await _unitOfWork.GetRepository<Depot>().AsQueryable()
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
        var consumableQuery = from dsi in _unitOfWork.GetRepository<SupplyInventory>().AsQueryable()
                              join ri    in _unitOfWork.GetRepository<ItemModel>().AsQueryable()  on dsi.ItemModelId equals ri.Id
                              join cat   in _unitOfWork.GetRepository<Category>().AsQueryable()  on ri.CategoryId  equals cat.Id
                              join depot in _unitOfWork.GetRepository<Depot>().AsQueryable()      on dsi.DepotId    equals depot.Id
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
                TotalQuantity          = x.dsi.Quantity ?? 0,
                ReservedQuantity       = x.dsi.ReservedQuantity ?? 0,
                AvailableQuantity      = (x.dsi.Quantity ?? 0) - (x.dsi.ReservedQuantity ?? 0),
                LastStockedAt          = x.dsi.LastStockedAt,
                GoodAvailableCount     = 0,  // N/A for Consumable
                FairAvailableCount     = 0,
                PoorAvailableCount     = 0
            })
            .ToListAsync(cancellationToken);

        // ── 2. Reusable rows — each physical unit tracked individually ────────
        var reusableQuery = from dri in _unitOfWork.GetRepository<ReusableItem>().AsQueryable()
                            join ri    in _unitOfWork.GetRepository<ItemModel>().AsQueryable()  on dri.ItemModelId equals ri.Id
                            join cat   in _unitOfWork.GetRepository<Category>().AsQueryable()  on ri.CategoryId  equals cat.Id
                            join depot in _unitOfWork.GetRepository<Depot>().AsQueryable()      on dri.DepotId    equals depot.Id
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
                Condition     = x.dri.Condition,
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
                    ItemModelId            = f.ItemModelId,
                    ItemModelName          = f.ItemModelName,
                    CategoryName           = f.CategoryName,
                    ItemType               = f.ItemType,
                    Unit                   = f.Unit,
                    DepotId                = f.DepotId,
                    DepotName              = f.DepotName,
                    DepotAddress           = f.DepotAddress,
                    DepotStatus            = f.DepotStatus,
                    DepotLocation          = f.DepotLocation,
                    TotalQuantity          = total,
                    ReservedQuantity       = total - available,
                    AvailableQuantity      = available,
                    LastStockedAt          = g.Max(x => x.CreatedAt),
                    GoodAvailableCount     = g.Count(x => x.IsAvailable && x.Condition == "Good"),
                    FairAvailableCount     = g.Count(x => x.IsAvailable && x.Condition == "Fair"),
                    PoorAvailableCount     = g.Count(x => x.IsAvailable && x.Condition == "Poor")
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
                TotalQuantity          = x.TotalQuantity,
                ReservedQuantity       = x.ReservedQuantity,
                AvailableQuantity      = x.AvailableQuantity,
                LastStockedAt          = x.LastStockedAt,
                GoodAvailableCount     = x.GoodAvailableCount,
                FairAvailableCount     = x.FairAvailableCount,
                PoorAvailableCount     = x.PoorAvailableCount
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

        var inventory = await _unitOfWork.GetRepository<SupplyInventory>().AsQueryable()
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

    public async Task ReserveSuppliesAsync(
        int depotId,
        List<(int ItemModelId, int Quantity)> items,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var (itemModelId, quantity) in items)
        {
            var inventory = await _unitOfWork.GetRepository<SupplyInventory>().AsQueryable(tracked: true)
                .FirstOrDefaultAsync(
                    x => x.DepotId == depotId && x.ItemModelId == itemModelId,
                    cancellationToken);

            if (inventory != null)
                inventory.ReservedQuantity = (inventory.ReservedQuantity ?? 0) + quantity;

            // Reusable items: Available → Reserved
            var reusableUnits = await _unitOfWork.GetRepository<ReusableItem>().AsQueryable(tracked: true)
                .Where(r => r.DepotId == depotId && r.ItemModelId == itemModelId && r.Status == "Available")
                .Take(quantity)
                .ToListAsync(cancellationToken);

            foreach (var unit in reusableUnits)
            {
                unit.Status    = "Reserved";
                unit.UpdatedAt = now;
            }
        }

        await _unitOfWork.SaveAsync();
    }

    public async Task ConsumeReservedSuppliesAsync(
        int depotId,
        List<(int ItemModelId, int Quantity)> items,
        Guid performedBy,
        int activityId,
        int missionId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var (itemModelId, quantity) in items)
        {
            var inventory = await _unitOfWork.GetRepository<SupplyInventory>().AsQueryable(tracked: true)
                .FirstOrDefaultAsync(
                    x => x.DepotId == depotId && x.ItemModelId == itemModelId,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Không tìm thấy tồn kho vật tư #{itemModelId} tại kho #{depotId}.");

            var currentQty      = inventory.Quantity         ?? 0;
            var currentReserved = inventory.ReservedQuantity ?? 0;

            if (currentReserved < quantity)
                throw new InvalidOperationException(
                    $"Vật tư #{itemModelId}: số lượng đặt trước ({currentReserved}) không đủ so với yêu cầu ({quantity}).");

            if (currentQty < quantity)
                throw new InvalidOperationException(
                    $"Vật tư #{itemModelId}: tồn kho thực ({currentQty}) không đủ so với yêu cầu ({quantity}).");

            inventory.Quantity         = currentQty      - quantity;
            inventory.ReservedQuantity = currentReserved - quantity;
            inventory.LastStockedAt    = now;

            // ── FEFO lot deduction ──────────────────────────────────────────
            var lots = await _unitOfWork.GetRepository<SupplyInventoryLot>().AsQueryable(tracked: true)
                .Where(l => l.SupplyInventoryId == inventory.Id
                         && l.RemainingQuantity > 0
                         && (!l.ExpiredDate.HasValue || l.ExpiredDate.Value >= now)) // skip expired lots
                .OrderBy(l => l.ExpiredDate == null ? 1 : 0)   // items WITH expiry first
                .ThenBy(l => l.ExpiredDate)                      // soonest expiry first (FEFO)
                .ThenBy(l => l.ReceivedDate)                     // oldest received first (FIFO)
                .ToListAsync(cancellationToken);

            if (lots.Count > 0)
            {
                var remaining = quantity;
                foreach (var lot in lots)
                {
                    if (remaining <= 0) break;
                    var deduct = Math.Min(lot.RemainingQuantity, remaining);
                    lot.RemainingQuantity -= deduct;
                    remaining -= deduct;

                    await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
                    {
                        DepotSupplyInventoryId = inventory.Id,
                        SupplyInventoryLotId   = lot.Id,
                        ActionType             = "MissionPickup",
                        QuantityChange         = deduct,
                        SourceType             = "MissionActivity",
                        SourceId               = activityId,
                        MissionId              = missionId,
                        PerformedBy            = performedBy,
                        Note                   = $"Xuất FEFO lô #{lot.Id} vật tư #{itemModelId} SL {deduct} cho activity #{activityId} (mission #{missionId})",
                        CreatedAt              = now
                    });
                }

                if (remaining > 0)
                    throw new InvalidOperationException(
                        $"Vật tư #{itemModelId}: không đủ lô chưa hết hạn để xuất {quantity} đơn vị.");
            }
            else
            {
                // Fallback: no lots yet (legacy data) — single log
                await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
                {
                    DepotSupplyInventoryId = inventory.Id,
                    ActionType             = "MissionPickup",
                    QuantityChange         = quantity,
                    SourceType             = "MissionActivity",
                    SourceId               = activityId,
                    MissionId              = missionId,
                    PerformedBy            = performedBy,
                    Note                   = $"Team xác nhận lấy hàng vật tư #{itemModelId} số lượng {quantity} cho activity #{activityId} (mission #{missionId})",
                    CreatedAt              = now
                });
            }

            // Reusable items: Reserved → InUse
            var reusableUnits = await _unitOfWork.GetRepository<ReusableItem>().AsQueryable(tracked: true)
                .Where(r => r.DepotId == depotId && r.ItemModelId == itemModelId && r.Status == "Reserved")
                .Take(quantity)
                .ToListAsync(cancellationToken);

            foreach (var unit in reusableUnits)
            {
                unit.Status    = "InUse";
                unit.UpdatedAt = now;
            }
        }

        await _unitOfWork.SaveAsync();
    }

    public async Task ReleaseReservedSuppliesAsync(
        int depotId,
        List<(int ItemModelId, int Quantity)> items,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var (itemModelId, quantity) in items)
        {
            var inventory = await _unitOfWork.GetRepository<SupplyInventory>().AsQueryable(tracked: true)
                .FirstOrDefaultAsync(
                    x => x.DepotId == depotId && x.ItemModelId == itemModelId,
                    cancellationToken);

            if (inventory != null)
                inventory.ReservedQuantity = Math.Max(0, (inventory.ReservedQuantity ?? 0) - quantity);

            // Reusable items: Reserved → Available
            var reusableUnits = await _unitOfWork.GetRepository<ReusableItem>().AsQueryable(tracked: true)
                .Where(r => r.DepotId == depotId && r.ItemModelId == itemModelId && r.Status == "Reserved")
                .Take(quantity)
                .ToListAsync(cancellationToken);

            foreach (var unit in reusableUnits)
            {
                unit.Status    = "Available";
                unit.UpdatedAt = now;
            }
        }

        await _unitOfWork.SaveAsync();
    }
}
