using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryByCategory;
using RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems;
using RESQ.Application.UseCases.Logistics.Queries.SearchWarehousesByItems;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Entities.Logistics.Models;
using RESQ.Domain.Entities.Logistics.Services;
using RESQ.Domain.Enum.Logistics;
using RESQ.Infrastructure.Entities.Logistics;
using DomainTargetGroup = RESQ.Domain.Enum.Logistics.TargetGroup;

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

    public async Task<Guid?> GetActiveManagerUserIdByDepotIdAsync(int depotId, CancellationToken ct = default)
    {
        var managerRecord = await _unitOfWork.GetRepository<DepotManager>()
            .GetByPropertyAsync(
                dm => dm.DepotId == depotId && dm.UnassignedAt == null,
                tracked: false
            );

        return managerRecord?.UserId;
    }

    public async Task<PagedResult<InventoryItemModel>> GetInventoryPagedAsync(
        int depotId,
        List<int>? categoryIds,
        List<ItemType>? itemTypes,
        List<DomainTargetGroup>? targetGroups,
        string? itemName,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedItemName = string.IsNullOrWhiteSpace(itemName) ? null : itemName.Trim().ToLowerInvariant();
        var hasItemNameFilter  = normalizedItemName is not null;
        // Captured as non-nullable so the compiler is satisfied inside LINQ expression trees.
        var itemNameFilter     = normalizedItemName ?? string.Empty;

        var safeCategoryIds = categoryIds ?? new List<int>();
        var hasCategoryFilter = safeCategoryIds.Count > 0;

        // No filter when itemTypes is null, empty, or contains ALL possible values (equivalent to no filter).
        var totalItemTypeCount = Enum.GetValues<ItemType>().Length;
        var itemTypeSet = (itemTypes != null && itemTypes.Count > 0)
            ? itemTypes.ToHashSet()
            : null;
        var hasItemTypeFilter = itemTypeSet != null && itemTypeSet.Count < totalItemTypeCount;

        var targetGroupSet = targetGroups?.Select(e => e.ToString().ToLower()).ToHashSet() ?? new HashSet<string>();
        var hasTargetGroupFilter = targetGroupSet.Count > 0;

        // Determine which tables to query based on the ItemType filter.
        // No filter (null/empty/all-types) → include both; explicit filter → include only matching types.
        bool includeConsumable = !hasItemTypeFilter || itemTypeSet!.Contains(ItemType.Consumable);
        bool includeReusable   = !hasItemTypeFilter || itemTypeSet!.Contains(ItemType.Reusable);

        var combined = new List<InventoryItemModel>();

        // ── Consumable items from depot_supply_inventory ──────────────────────
        if (includeConsumable)
        {
            var consumableRaw = await (
                from inv in _unitOfWork.Set<SupplyInventory>()
                join ri  in _unitOfWork.Set<ItemModel>() on inv.ItemModelId equals ri.Id
                where inv.DepotId == depotId
                         && ri.ItemType == "Consumable"
                   && (!hasCategoryFilter   || safeCategoryIds.Contains(ri.CategoryId ?? 0))
                   && (!hasTargetGroupFilter || ri.TargetGroups.Any(tg => targetGroupSet.Contains(tg.Name.ToLower())))
                   && (!hasItemNameFilter   || (ri.Name ?? string.Empty).ToLower().Contains(itemNameFilter))
                select new
                {
                    ri.Id, ri.Name, ri.ImageUrl, ri.CategoryId, ri.ItemType,
                    Quantity                 = inv.Quantity                 ?? 0,
                    MissionReservedQuantity  = inv.MissionReservedQuantity,
                    TransferReservedQuantity = inv.TransferReservedQuantity,
                    LastStockedAt            = inv.LastStockedAt
                }
            ).ToListAsync(cancellationToken);

            // Fetch TargetGroups for consumable ItemModels in one query
            var consumableItemModelIds = consumableRaw.Select(x => x.Id).Distinct().ToList();
            var consumableTgDict = await _unitOfWork.Set<ItemModel>()
                .Include(r => r.TargetGroups)
                .Where(r => consumableItemModelIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, r => r.TargetGroups.Select(tg => TargetGroupTranslations.ToVietnamese(tg.Name)).ToList(), cancellationToken);

            var catIds = consumableRaw.Where(x => x.CategoryId.HasValue).Select(x => x.CategoryId!.Value).Distinct().ToList();
            var catDict = await _unitOfWork.Set<Category>()
                .Where(c => catIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Name ?? string.Empty, cancellationToken);

            // Lot summary per item model (FEFO info)
            var lotSummaries = await (
                from lot in _unitOfWork.Set<SupplyInventoryLot>()
                join inv in _unitOfWork.Set<SupplyInventory>() on lot.SupplyInventoryId equals inv.Id
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
                    ImageUrl       = x.ImageUrl,
                    CategoryId     = x.CategoryId,
                    CategoryName   = x.CategoryId.HasValue && catDict.TryGetValue(x.CategoryId.Value, out var cn) ? cn : string.Empty,
                    ItemType       = x.ItemType,
                    TargetGroups   = consumableTgDict.TryGetValue(x.Id, out var tgNames) ? tgNames : new List<string>(),
                    Availability   = _inventoryQueryService.ComputeAvailability(x.Quantity, x.MissionReservedQuantity, x.TransferReservedQuantity),
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
                from dri in _unitOfWork.Set<ReusableItem>()
                join ri  in _unitOfWork.Set<ItemModel>() on dri.ItemModelId equals ri.Id
                where dri.DepotId == depotId
                   && (!hasCategoryFilter   || safeCategoryIds.Contains(ri.CategoryId ?? 0))
                   && (!hasTargetGroupFilter || ri.TargetGroups.Any(tg => targetGroupSet.Contains(tg.Name.ToLower())))
                   && (!hasItemNameFilter   || (ri.Name ?? string.Empty).ToLower().Contains(itemNameFilter))
                group new { dri, ri } by new { ri.Id, ri.Name, ri.ImageUrl, ri.CategoryId, ri.ItemType } into g
                select new
                {
                    Id                  = g.Key.Id,
                    Name                = g.Key.Name,
                    ImageUrl            = g.Key.ImageUrl,
                    CategoryId          = g.Key.CategoryId,
                    ItemType            = g.Key.ItemType,
                    TotalUnits               = g.Count(),
                    AvailableUnits           = g.Count(x => x.dri.Status == nameof(ReusableItemStatus.Available)),
                    ReservedForMissionUnits  = g.Count(x => x.dri.Status == nameof(ReusableItemStatus.Reserved) && x.dri.SupplyRequestId == null),
                    ReservedForTransferUnits = g.Count(x => x.dri.Status == nameof(ReusableItemStatus.Reserved) && x.dri.SupplyRequestId != null),
                    InTransitUnits           = g.Count(x => x.dri.Status == nameof(ReusableItemStatus.InTransit)),
                    InUseUnits               = g.Count(x => x.dri.Status == nameof(ReusableItemStatus.InUse)),
                    MaintenanceUnits         = g.Count(x => x.dri.Status == nameof(ReusableItemStatus.Maintenance)),
                    DecommissionedUnits      = g.Count(x => x.dri.Status == nameof(ReusableItemStatus.Decommissioned)),
                    GoodCount                = g.Count(x => x.dri.Condition == "Good"),
                    FairCount                = g.Count(x => x.dri.Condition == "Fair"),
                    PoorCount                = g.Count(x => x.dri.Condition == "Poor"),
                    LastStockedAt            = g.Max(x => x.dri.CreatedAt)
                }
            ).ToListAsync(cancellationToken);

            var catIds = reusableRaw.Where(x => x.CategoryId.HasValue).Select(x => x.CategoryId!.Value).Distinct().ToList();
            var catDict = await _unitOfWork.Set<Category>()
                .Where(c => catIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Name ?? string.Empty, cancellationToken);

            // Fetch TargetGroups for each reusable ItemModel
            var reusableItemModelIds = reusableRaw.Select(x => x.Id).Distinct().ToList();
            var reusableTgDict = await _unitOfWork.Set<ItemModel>()
                .Include(r => r.TargetGroups)
                .Where(r => reusableItemModelIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, r => r.TargetGroups.Select(tg => TargetGroupTranslations.ToVietnamese(tg.Name)).ToList(), cancellationToken);

            combined.AddRange(reusableRaw.Select(x => new InventoryItemModel
            {
                ItemModelId      = x.Id,
                ItemModelName    = x.Name ?? string.Empty,
                ImageUrl         = x.ImageUrl,
                CategoryId        = x.CategoryId,
                CategoryName      = x.CategoryId.HasValue && catDict.TryGetValue(x.CategoryId.Value, out var cn) ? cn : string.Empty,
                ItemType          = x.ItemType,
                TargetGroups      = reusableTgDict.TryGetValue(x.Id, out var tgNames) ? tgNames : new List<string>(),
                Availability      = _inventoryQueryService.ComputeAvailability(
                                        x.TotalUnits,
                                        x.ReservedForMissionUnits,
                                        x.ReservedForTransferUnits),
                LastStockedAt     = x.LastStockedAt,
                ReusableBreakdown = new RESQ.Domain.Entities.Logistics.ValueObjects.ReusableBreakdown
                {
                    TotalUnits               = x.TotalUnits,
                    AvailableUnits           = x.AvailableUnits,
                    ReservedForMissionUnits  = x.ReservedForMissionUnits,
                    ReservedForTransferUnits = x.ReservedForTransferUnits,
                    InTransitUnits           = x.InTransitUnits,
                    InUseUnits               = x.InUseUnits,
                    MaintenanceUnits         = x.MaintenanceUnits,
                    DecommissionedUnits      = x.DecommissionedUnits,
                    GoodCount                = x.GoodCount,
                    FairCount                = x.FairCount,
                    PoorCount                = x.PoorCount
                }
            }));
        }

        var totalCount = combined.Count;
        var pagedItems = combined
            .OrderByDescending(x => x.LastStockedAt)
            .ThenBy(x => x.ItemModelId)   // stable tiebreaker: giữ thứ tự cố định khi LastStockedAt bằng nhau
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<InventoryItemModel>(pagedItems, totalCount, pageNumber, pageSize);
    }

    public async Task<PagedResult<InventoryLotModel>> GetInventoryLotsAsync(
        int depotId, int itemModelId, int pageNumber, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = from lot in _unitOfWork.Set<SupplyInventoryLot>()
                    join inv in _unitOfWork.Set<SupplyInventory>() on lot.SupplyInventoryId equals inv.Id
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
        IReadOnlyCollection<int>? allowedDepotIds = null,
        CancellationToken ct = default)
    {
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Max(pageSize, 1);
        var takeFromEachSource = safePage * safePageSize;
        var allowedDepotIdList = allowedDepotIds?
            .Where(id => id > 0)
            .Distinct()
            .ToList() ?? [];

        if (allowedDepotIds is not null && allowedDepotIdList.Count == 0)
            return ([], 0);

        var categoryPattern = $"%{categoryKeyword.Trim()}%";
        var typePattern = string.IsNullOrWhiteSpace(typeKeyword)
            ? null
            : $"%{typeKeyword.Trim()}%";

        var consumableQuery = from dsi in _unitOfWork.Set<SupplyInventory>()
                              join ri in _unitOfWork.Set<ItemModel>() on dsi.ItemModelId equals ri.Id
                              join cat in _unitOfWork.Set<Category>() on ri.CategoryId equals cat.Id
                              join depot in _unitOfWork.Set<Depot>() on dsi.DepotId equals depot.Id
                              where depot.Status == "Available"
                                    && ri.ItemType == nameof(ItemType.Consumable)
                                    && (dsi.Quantity ?? 0) - (dsi.MissionReservedQuantity + dsi.TransferReservedQuantity) > 0
                                    && EF.Functions.ILike(cat.Name ?? string.Empty, categoryPattern)
                              select new AgentInventorySearchRow
                              {
                                  ItemId = ri.Id,
                                  ItemName = ri.Name ?? string.Empty,
                                  CategoryName = cat.Name ?? string.Empty,
                                  ItemType = ri.ItemType,
                                  Unit = ri.Unit,
                                  AvailableQuantity = (dsi.Quantity ?? 0) - (dsi.MissionReservedQuantity + dsi.TransferReservedQuantity),
                                  DepotId = depot.Id,
                                  DepotName = depot.Name ?? string.Empty,
                                  DepotAddress = depot.Address,
                                  GoodAvailableCount = null,
                                  FairAvailableCount = null,
                                  PoorAvailableCount = null
                              };

        if (allowedDepotIdList.Count > 0)
            consumableQuery = consumableQuery.Where(x => allowedDepotIdList.Contains(x.DepotId));

        if (typePattern is not null)
        {
            consumableQuery = consumableQuery.Where(x =>
                EF.Functions.ILike(x.ItemName, typePattern)
                || EF.Functions.ILike(x.ItemType ?? string.Empty, typePattern));
        }

        var reusableBaseQuery = from reusable in _unitOfWork.Set<ReusableItem>()
                                join ri in _unitOfWork.Set<ItemModel>() on reusable.ItemModelId equals ri.Id
                                join cat in _unitOfWork.Set<Category>() on ri.CategoryId equals cat.Id
                                join depot in _unitOfWork.Set<Depot>() on reusable.DepotId equals depot.Id
                                where reusable.DepotId.HasValue
                                      && reusable.ItemModelId.HasValue
                                      && depot.Status == "Available"
                                      && ri.ItemType == nameof(ItemType.Reusable)
                                      && reusable.Status == nameof(ReusableItemStatus.Available)
                                      && EF.Functions.ILike(cat.Name ?? string.Empty, categoryPattern)
                                select new { reusable, ri, cat, depot };

        if (allowedDepotIdList.Count > 0)
            reusableBaseQuery = reusableBaseQuery.Where(x => allowedDepotIdList.Contains(x.depot.Id));

        if (typePattern is not null)
        {
            reusableBaseQuery = reusableBaseQuery.Where(x =>
                EF.Functions.ILike(x.ri.Name ?? string.Empty, typePattern)
                || EF.Functions.ILike(x.ri.ItemType ?? string.Empty, typePattern));
        }

        var reusableQuery = reusableBaseQuery
            .GroupBy(x => new
            {
                ItemId = x.ri.Id,
                ItemName = x.ri.Name ?? string.Empty,
                CategoryName = x.cat.Name ?? string.Empty,
                ItemType = x.ri.ItemType,
                Unit = x.ri.Unit,
                DepotId = x.depot.Id,
                DepotName = x.depot.Name ?? string.Empty,
                DepotAddress = x.depot.Address
            })
            .Select(group => new AgentInventorySearchRow
            {
                ItemId = group.Key.ItemId,
                ItemName = group.Key.ItemName,
                CategoryName = group.Key.CategoryName,
                ItemType = group.Key.ItemType,
                Unit = group.Key.Unit,
                AvailableQuantity = group.Count(),
                DepotId = group.Key.DepotId,
                DepotName = group.Key.DepotName,
                DepotAddress = group.Key.DepotAddress,
                GoodAvailableCount = group.Count(x => x.reusable.Condition == nameof(ReusableItemCondition.Good)),
                FairAvailableCount = group.Count(x => x.reusable.Condition == nameof(ReusableItemCondition.Fair)),
                PoorAvailableCount = group.Count(x => x.reusable.Condition == nameof(ReusableItemCondition.Poor))
            });

        var consumableTotal = await consumableQuery.CountAsync(ct);
        var reusableTotal = await reusableQuery.CountAsync(ct);

        var consumableRows = await consumableQuery
            .OrderByDescending(x => x.AvailableQuantity)
            .ThenBy(x => x.ItemName)
            .ThenBy(x => x.DepotName)
            .ThenBy(x => x.ItemId)
            .ThenBy(x => x.DepotId)
            .Take(takeFromEachSource)
            .ToListAsync(ct);

        var reusableRows = await reusableQuery
            .OrderByDescending(x => x.AvailableQuantity)
            .ThenByDescending(x => x.GoodAvailableCount ?? 0)
            .ThenByDescending(x => x.FairAvailableCount ?? 0)
            .ThenBy(x => x.ItemName)
            .ThenBy(x => x.DepotName)
            .ThenBy(x => x.ItemId)
            .ThenBy(x => x.DepotId)
            .Take(takeFromEachSource)
            .ToListAsync(ct);

        var mergedRows = consumableRows
            .Concat(reusableRows)
            .OrderByDescending(x => x.AvailableQuantity)
            .ThenByDescending(x => x.GoodAvailableCount ?? 0)
            .ThenByDescending(x => x.FairAvailableCount ?? 0)
            .ThenBy(x => x.ItemName)
            .ThenBy(x => x.DepotName)
            .ThenBy(x => x.ItemId)
            .ThenBy(x => x.DepotId)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToList();

        var depotLocationLookup = await LoadDepotLocationLookupAsync(
            mergedRows.Select(x => x.DepotId).Distinct().ToList(),
            ct);

        var items = mergedRows.Select(x =>
        {
            depotLocationLookup.TryGetValue(x.DepotId, out var depotCoordinates);
            return new AgentInventoryItem
            {
                ItemId = x.ItemId,
                ItemName = x.ItemName,
                CategoryName = x.CategoryName,
                ItemType = x.ItemType,
                Unit = x.Unit,
                AvailableQuantity = x.AvailableQuantity,
                GoodAvailableCount = x.GoodAvailableCount,
                FairAvailableCount = x.FairAvailableCount,
                PoorAvailableCount = x.PoorAvailableCount,
                DepotId = x.DepotId,
                DepotName = x.DepotName,
                DepotAddress = x.DepotAddress,
                DepotLatitude = depotCoordinates.Latitude,
                DepotLongitude = depotCoordinates.Longitude
            };
        }).ToList();

        return (items, consumableTotal + reusableTotal);
    }

    private async Task<Dictionary<int, (double? Latitude, double? Longitude)>> LoadDepotLocationLookupAsync(
        IReadOnlyCollection<int> depotIds,
        CancellationToken cancellationToken)
    {
        if (depotIds.Count == 0)
            return [];

        var depots = await _unitOfWork.Set<Depot>()
            .Where(depot => depotIds.Contains(depot.Id))
            .Select(depot => new
            {
                depot.Id,
                depot.Location
            })
            .ToListAsync(cancellationToken);

        return depots.ToDictionary(
            depot => depot.Id,
            depot => ((double?)depot.Location?.Y, (double?)depot.Location?.X));
    }

    private sealed class AgentInventorySearchRow
    {
        public int ItemId { get; init; }
        public string ItemName { get; init; } = string.Empty;
        public string CategoryName { get; init; } = string.Empty;
        public string? ItemType { get; init; }
        public string? Unit { get; init; }
        public int AvailableQuantity { get; init; }
        public int DepotId { get; init; }
        public string DepotName { get; init; } = string.Empty;
        public string? DepotAddress { get; init; }
        public int? GoodAvailableCount { get; init; }
        public int? FairAvailableCount { get; init; }
        public int? PoorAvailableCount { get; init; }
    }

    public async Task<List<DepotCategoryQuantityDto>> GetInventoryByCategoryAsync(int depotId, CancellationToken cancellationToken = default)
    {
        var categories = _unitOfWork.Set<Category>();
        var supplyInventories = _unitOfWork.Set<SupplyInventory>();
        var itemModels = _unitOfWork.Set<ItemModel>();
        var reusableItems = _unitOfWork.Set<ReusableItem>();

        var result = await (
            from cat in categories
            orderby cat.Id
            select new DepotCategoryQuantityDto
            {
                CategoryId   = cat.Id,
                CategoryCode = cat.Code ?? string.Empty,
                CategoryName = cat.Name ?? string.Empty,
                // Consumable items: sum of quantities in supply_inventory
                TotalConsumableQuantity = (
                    from dsi in supplyInventories
                    join ri in itemModels on dsi.ItemModelId equals ri.Id
                    where dsi.DepotId == depotId && ri.CategoryId == cat.Id && ri.ItemType == "Consumable"
                    select (int?)dsi.Quantity
                ).Sum() ?? 0,
                // Consumable items: available = Quantity - TotalReservedQuantity
                AvailableConsumableQuantity = (
                    from dsi in supplyInventories
                    join ri in itemModels on dsi.ItemModelId equals ri.Id
                    where dsi.DepotId == depotId && ri.CategoryId == cat.Id && ri.ItemType == "Consumable"
                    select (int?)(dsi.Quantity - (dsi.MissionReservedQuantity + dsi.TransferReservedQuantity))
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
        var location = await _unitOfWork.Set<Depot>()
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
        var consumableQuery = from dsi in _unitOfWork.Set<SupplyInventory>()
                              join ri    in _unitOfWork.Set<ItemModel>()  on dsi.ItemModelId equals ri.Id
                              join cat   in _unitOfWork.Set<Category>()  on ri.CategoryId  equals cat.Id
                              join depot in _unitOfWork.Set<Depot>()      on dsi.DepotId    equals depot.Id
                              where ri.ItemType == "Consumable"
                              select new { dsi, ri, cat, depot };

        if (excludeDepotId.HasValue)
            consumableQuery = consumableQuery.Where(x => x.depot.Id != excludeDepotId.Value);
        if (activeDepotsOnly)
            consumableQuery = consumableQuery.Where(x => x.depot.Status == "Available");
        if (hasIdFilter)
            consumableQuery = consumableQuery.Where(x => safeIds.Contains(x.ri.Id));
        consumableQuery = consumableQuery.Where(x => (x.dsi.Quantity ?? 0) - (x.dsi.MissionReservedQuantity + x.dsi.TransferReservedQuantity) >= 1);

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
                ReservedQuantity  = x.dsi.MissionReservedQuantity + x.dsi.TransferReservedQuantity,
                AvailableQuantity = (x.dsi.Quantity ?? 0) - (x.dsi.MissionReservedQuantity + x.dsi.TransferReservedQuantity),
                LastStockedAt          = x.dsi.LastStockedAt,
                GoodAvailableCount     = 0,  // N/A for Consumable
                FairAvailableCount     = 0,
                PoorAvailableCount     = 0
            })
            .ToListAsync(cancellationToken);

        // ── 2. Reusable rows — each physical unit tracked individually ────────
        var reusableQuery = from dri in _unitOfWork.Set<ReusableItem>()
                            join ri    in _unitOfWork.Set<ItemModel>()  on dri.ItemModelId equals ri.Id
                            join cat   in _unitOfWork.Set<Category>()  on ri.CategoryId  equals cat.Id
                            join depot in _unitOfWork.Set<Depot>()      on dri.DepotId    equals depot.Id
                            select new { dri, ri, cat, depot };

        if (excludeDepotId.HasValue)
            reusableQuery = reusableQuery.Where(x => x.depot.Id != excludeDepotId.Value);
        if (activeDepotsOnly)
            reusableQuery = reusableQuery.Where(x => x.depot.Status == "Available");
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

        // Determine which item IDs are Reusable so we route to the correct availability source.
        var reusableItemModelIds = (await _unitOfWork.Set<ItemModel>().AsNoTracking()
            .Where(im => itemModelIds.Contains(im.Id) && im.ItemType == "Reusable")
            .Select(im => im.Id)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        // Consumable: net available = storedQty − missionReserved − transferReserved
        var consumableAvailability = await (
            from inv in _unitOfWork.Set<SupplyInventory>().AsNoTracking()
            join im in _unitOfWork.Set<ItemModel>().AsNoTracking() on inv.ItemModelId equals im.Id
            where inv.DepotId == depotId
               && inv.ItemModelId != null
               && itemModelIds.Contains(inv.ItemModelId!.Value)
               && im.ItemType == "Consumable"
            select new
            {
                ItemModelId = inv.ItemModelId!.Value,
                Available = (inv.Quantity ?? 0) - (inv.MissionReservedQuantity + inv.TransferReservedQuantity)
            })
            .ToDictionaryAsync(x => x.ItemModelId, x => x.Available, cancellationToken);

        // Reusable: available = count of per-unit rows with Status == "Available".
        // Distinguishes "no units registered in this depot" (TotalUnits == 0, NotFound)
        // from "all units currently in use" (AvailableUnits < requested, shortage).
        var reusableStats = await _unitOfWork.Set<ReusableItem>().AsNoTracking()
            .Where(r => r.DepotId == depotId
                     && r.ItemModelId != null
                     && itemModelIds.Contains(r.ItemModelId!.Value))
            .GroupBy(r => r.ItemModelId!.Value)
            .Select(g => new
            {
                ItemModelId    = g.Key,
                TotalUnits     = g.Count(),
                AvailableUnits = g.Count(r => r.Status == nameof(ReusableItemStatus.Available))
            })
            .ToDictionaryAsync(x => x.ItemModelId, cancellationToken);

        var shortages = new List<SupplyShortageResult>();

        foreach (var (itemModelId, itemName, requestedQty) in items)
        {
            if (reusableItemModelIds.Contains(itemModelId))
            {
                // Reusable: per-unit status tracking (not bulk quantity)
                if (!reusableStats.TryGetValue(itemModelId, out var stat) || stat.TotalUnits == 0)
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
                else if (stat.AvailableUnits < requestedQty)
                {
                    shortages.Add(new SupplyShortageResult
                    {
                        ItemModelId = itemModelId,
                        ItemName = itemName,
                        RequestedQuantity = requestedQty,
                        AvailableQuantity = stat.AvailableUnits,
                        NotFound = false
                    });
                }
            }
            else
            {
                // Consumable: bulk quantity tracked in SupplyInventory
                if (!consumableAvailability.TryGetValue(itemModelId, out var available))
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
        }

        return shortages;
    }

    public async Task<MissionSupplyReservationResult> ReserveSuppliesAsync(
        int depotId,
        List<(int ItemModelId, int Quantity)> items,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var itemIds = items.Select(x => x.ItemModelId).Distinct().ToList();
        var itemLookup = await _unitOfWork.Set<ItemModel>()
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, cancellationToken);
        var reservationItems = new List<SupplyExecutionItemDto>(items.Count);

        foreach (var (itemModelId, quantity) in items)
        {
            if (!itemLookup.TryGetValue(itemModelId, out var itemModel))
                throw new InvalidOperationException($"Không tìm thấy metadata vật tư #{itemModelId}.");

            var isReusable = string.Equals(itemModel.ItemType, "Reusable", StringComparison.OrdinalIgnoreCase);
            var reservationItem = new SupplyExecutionItemDto
            {
                ItemModelId = itemModelId,
                ItemName = itemModel.Name ?? string.Empty,
                Unit = itemModel.Unit,
                Quantity = quantity
            };

            if (!isReusable)
            {
                var inventory = await _unitOfWork.SetTracked<SupplyInventory>()
                    .FirstOrDefaultAsync(
                        x => x.DepotId == depotId && x.ItemModelId == itemModelId,
                        cancellationToken);

                if (inventory != null)
                {
                    var plannedLots = await BuildReservedLotPlanAsync(
                        inventory.Id,
                        inventory.MissionReservedQuantity,
                        quantity,
                        now,
                        cancellationToken);

                    inventory.MissionReservedQuantity += quantity;

                    reservationItem.LotAllocations.AddRange(plannedLots);
                }

                reservationItems.Add(reservationItem);
                continue;
            }

            // Reusable items: Available → Reserved (mission — no SupplyRequestId)
            var reusableUnits = await _unitOfWork.SetTracked<ReusableItem>()
                .Where(r => r.DepotId == depotId && r.ItemModelId == itemModelId && r.Status == nameof(ReusableItemStatus.Available))
                .OrderBy(r => r.Id)
                .Take(quantity)
                .ToListAsync(cancellationToken);

            if (isReusable && reusableUnits.Count < quantity)
                throw new InvalidOperationException(
                    $"Vật tư reusable #{itemModelId}: chỉ còn {reusableUnits.Count} đơn vị Available trong khi cần reserve {quantity}.");

            foreach (var unit in reusableUnits)
            {
                unit.Status    = nameof(ReusableItemStatus.Reserved);
                unit.UpdatedAt = now;

                reservationItem.ReusableUnits.Add(new SupplyExecutionReusableUnitDto
                {
                    ReusableItemId = unit.Id,
                    ItemModelId = itemModelId,
                    ItemName = itemModel.Name ?? string.Empty,
                    SerialNumber = unit.SerialNumber,
                    Condition = unit.Condition
                });
            }

            reservationItems.Add(reservationItem);
        }

        await _unitOfWork.SaveAsync();

        return new MissionSupplyReservationResult
        {
            Items = reservationItems
        };
    }

    public async Task<MissionSupplyPickupExecutionResult> ConsumeReservedSuppliesAsync(
        int depotId,
        List<(int ItemModelId, int Quantity)> items,
        Guid performedBy,
        int activityId,
        int missionId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var itemIds = items.Select(x => x.ItemModelId).Distinct().ToList();
        var itemLookup = await _unitOfWork.Set<ItemModel>()
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, cancellationToken);
        var executionItems = new List<SupplyExecutionItemDto>(items.Count);

        foreach (var (itemModelId, quantity) in items)
        {
            if (!itemLookup.TryGetValue(itemModelId, out var itemModel))
                throw new InvalidOperationException($"Không tìm thấy metadata vật tư #{itemModelId}.");

            var isReusable = string.Equals(itemModel.ItemType, "Reusable", StringComparison.OrdinalIgnoreCase);
            var executionItem = new SupplyExecutionItemDto
            {
                ItemModelId = itemModelId,
                ItemName = itemModel.Name ?? string.Empty,
                Unit = itemModel.Unit,
                Quantity = quantity
            };

            if (!isReusable)
            {
                var inventory = await _unitOfWork.SetTracked<SupplyInventory>()
                    .FirstOrDefaultAsync(
                        x => x.DepotId == depotId && x.ItemModelId == itemModelId,
                        cancellationToken)
                    ?? throw new InvalidOperationException(
                        $"Không tìm thấy tồn kho vật tư #{itemModelId} tại kho #{depotId}.");

                var currentQty      = inventory.Quantity             ?? 0;
                var currentReserved = inventory.MissionReservedQuantity;

                if (currentReserved < quantity)
                    throw new InvalidOperationException(
                        $"Vật tư #{itemModelId}: số lượng đặt trước nhiệm vụ ({currentReserved}) không đủ so với yêu cầu ({quantity}).");

                if (currentQty < quantity)
                    throw new InvalidOperationException(
                        $"Vật tư #{itemModelId}: tồn kho thực ({currentQty}) không đủ so với yêu cầu ({quantity}).");

                inventory.Quantity                = currentQty      - quantity;
                inventory.MissionReservedQuantity = currentReserved - quantity;
                inventory.LastStockedAt           = now;

                // ── FEFO lot deduction ──────────────────────────────────────────
                var lots = await _unitOfWork.SetTracked<SupplyInventoryLot>()
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

                        executionItem.LotAllocations.Add(new SupplyExecutionLotDto
                        {
                            LotId = lot.Id,
                            QuantityTaken = deduct,
                            ReceivedDate = lot.ReceivedDate,
                            ExpiredDate = lot.ExpiredDate,
                            RemainingQuantityAfterExecution = lot.RemainingQuantity
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
            }

            // Reusable items: Reserved → InUse
            if (isReusable)
            {
                var reusableUnits = await _unitOfWork.SetTracked<ReusableItem>()
                .Where(r => r.DepotId == depotId && r.ItemModelId == itemModelId && r.Status == nameof(ReusableItemStatus.Reserved) && r.SupplyRequestId == null)
                .OrderBy(r => r.Id)
                .Take(quantity)
                .ToListAsync(cancellationToken);

            if (reusableUnits.Count < quantity)
                throw new InvalidOperationException(
                    $"Vật tư reusable #{itemModelId}: chỉ tìm thấy {reusableUnits.Count} đơn vị Reserved trong khi cần {quantity}.");

            foreach (var unit in reusableUnits)
            {
                unit.Status    = nameof(ReusableItemStatus.InUse);
                unit.UpdatedAt = now;

                await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
                {
                    ReusableItemId = unit.Id,
                    ActionType     = "MissionPickup",
                    QuantityChange = 1,
                    SourceType     = "MissionActivity",
                    SourceId       = activityId,
                    MissionId      = missionId,
                    PerformedBy    = performedBy,
                    Note           = $"Team xác nhận lấy {itemModel.Name} (S/N: {unit.SerialNumber ?? "N/A"}) cho activity #{activityId} (mission #{missionId})",
                    CreatedAt      = now
                });

                executionItem.ReusableUnits.Add(new SupplyExecutionReusableUnitDto
                {
                    ReusableItemId = unit.Id,
                    ItemModelId = itemModelId,
                    ItemName = itemModel.Name ?? string.Empty,
                    SerialNumber = unit.SerialNumber,
                    Condition = unit.Condition
                });
            }

            }

            executionItems.Add(executionItem);
        }

        await _unitOfWork.SaveAsync();

        return new MissionSupplyPickupExecutionResult
        {
            Items = executionItems
        };
    }

    public async Task ReleaseReservedSuppliesAsync(
        int depotId,
        List<(int ItemModelId, int Quantity)> items,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var itemIds = items.Select(x => x.ItemModelId).Distinct().ToList();
        var itemTypeDict = await _unitOfWork.Set<ItemModel>()
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.ItemType ?? string.Empty, cancellationToken);

        foreach (var (itemModelId, quantity) in items)
        {
            var isReusable = itemTypeDict.TryGetValue(itemModelId, out var itemType)
                && string.Equals(itemType, "Reusable", StringComparison.OrdinalIgnoreCase);

            if (!isReusable)
            {
                var inventory = await _unitOfWork.SetTracked<SupplyInventory>()
                    .FirstOrDefaultAsync(
                        x => x.DepotId == depotId && x.ItemModelId == itemModelId,
                        cancellationToken);

                if (inventory != null)
                {
                    if (inventory.MissionReservedQuantity < quantity)
                        throw new InvalidOperationException(
                            $"Vật tư #{itemModelId}: không thể giải phóng {quantity} đơn vị, " +
                            $"mission_reserved_quantity hiện là {inventory.MissionReservedQuantity}.");

                    inventory.MissionReservedQuantity -= quantity;
                }

                continue;
            }

            // Reusable items: Reserved → Available
            var reusableUnits = await _unitOfWork.SetTracked<ReusableItem>()
                .Where(r => r.DepotId == depotId && r.ItemModelId == itemModelId
                            && r.Status == nameof(ReusableItemStatus.Reserved)
                            && r.SupplyRequestId == null)
                .Take(quantity)
                .ToListAsync(cancellationToken);

            foreach (var unit in reusableUnits)
            {
                unit.Status    = nameof(ReusableItemStatus.Available);
                unit.UpdatedAt = now;
            }
        }

        await _unitOfWork.SaveAsync();
    }

    public async Task<List<LowStockRawItemDto>> GetLowStockRawItemsAsync(
        int? depotId,
        CancellationToken cancellationToken = default)
    {
        var query =
            from inv  in _unitOfWork.Set<SupplyInventory>()
            join item in _unitOfWork.Set<ItemModel>()     on inv.ItemModelId equals item.Id
            join depot in _unitOfWork.Set<Depot>()        on inv.DepotId     equals depot.Id
            join cat  in _unitOfWork.Set<Category>()      on item.CategoryId equals cat.Id into catJoin
            from cat in catJoin.DefaultIfEmpty()
            where item.ItemType == "Consumable"
               && (depotId == null || inv.DepotId == depotId)
            select new
            {
                DepotId          = depot.Id,
                DepotName        = depot.Name ?? string.Empty,
                ItemModelId      = item.Id,
                ItemModelName    = item.Name ?? string.Empty,
                Unit             = item.Unit,
                CategoryId       = item.CategoryId,
                CategoryName     = cat != null ? cat.Name ?? string.Empty : string.Empty,
                Quantity         = inv.Quantity ?? 0,
                ReservedQuantity = inv.MissionReservedQuantity + inv.TransferReservedQuantity,
                Available        = (inv.Quantity ?? 0) - (inv.MissionReservedQuantity + inv.TransferReservedQuantity)
            };

        var raw = await query
            .OrderBy(x => x.DepotId)
            .ThenBy(x => x.ItemModelId)
            .ToListAsync(cancellationToken);

        // Fetch TargetGroups for all item models in one query
        var lowStockItemModelIds = raw.Select(r => r.ItemModelId).Distinct().ToList();
        var lowStockTgDict = await _unitOfWork.Set<ItemModel>()
            .Include(r => r.TargetGroups)
            .Where(r => lowStockItemModelIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => TargetGroupTranslations.JoinAsVietnamese(r.TargetGroups.Select(tg => tg.Name)), cancellationToken);

        return raw.Select(r => new LowStockRawItemDto
        {
            DepotId = r.DepotId,
            DepotName = r.DepotName,
            ItemModelId = r.ItemModelId,
            ItemModelName = r.ItemModelName,
            Unit = r.Unit,
            CategoryId = r.CategoryId,
            CategoryName = r.CategoryName,
            TargetGroup = lowStockTgDict.TryGetValue(r.ItemModelId, out var tgStr) ? tgStr : null,
            Quantity = r.Quantity,
            ReservedQuantity = r.ReservedQuantity,
            AvailableQuantity = r.Available
        }).ToList();
    }

    public async Task ExportInventoryAsync(
        int depotId,
        int itemModelId,
        int quantity,
        Guid performedBy,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var inventory = await _unitOfWork.SetTracked<SupplyInventory>()
            .FirstOrDefaultAsync(x => x.DepotId == depotId && x.ItemModelId == itemModelId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Không tìm thấy tồn kho vật tư #{itemModelId} tại kho #{depotId}.");

        var available = (inventory.Quantity ?? 0) - (inventory.MissionReservedQuantity + inventory.TransferReservedQuantity);
        if (available < quantity)
            throw new InvalidOperationException(
                $"Vật tư #{itemModelId}: số lượng khả dụng ({available}) không đủ so với yêu cầu xuất ({quantity}).");

        inventory.Quantity      = (inventory.Quantity ?? 0) - quantity;
        inventory.LastStockedAt = now;

        // ── FEFO lot deduction ──────────────────────────────────────────
        var lots = await _unitOfWork.SetTracked<SupplyInventoryLot>()
            .Where(l => l.SupplyInventoryId == inventory.Id && l.RemainingQuantity > 0)
            .OrderBy(l => l.ExpiredDate == null ? 1 : 0)  // items WITH expiry first
            .ThenBy(l => l.ExpiredDate)                    // soonest expiry first (FEFO)
            .ThenBy(l => l.ReceivedDate)                   // oldest received first (FIFO tie-breaker)
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
                    ActionType             = InventoryActionType.Export.ToString(),
                    QuantityChange         = deduct,
                    SourceType             = InventorySourceType.System.ToString(),
                    PerformedBy            = performedBy,
                    Note                   = !string.IsNullOrWhiteSpace(note)
                        ? note
                        : $"Xuất kho FEFO lô #{lot.Id} vật tư #{itemModelId} SL {deduct}",
                    CreatedAt              = now
                });
            }

            if (remaining > 0)
                throw new InvalidOperationException(
                    $"Vật tư #{itemModelId}: không đủ lô để xuất {quantity} đơn vị.");
        }
        else
        {
            // Fallback: no lots yet (legacy data) — single log
            await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
            {
                DepotSupplyInventoryId = inventory.Id,
                ActionType             = InventoryActionType.Export.ToString(),
                QuantityChange         = quantity,
                SourceType             = InventorySourceType.System.ToString(),
                PerformedBy            = performedBy,
                Note                   = !string.IsNullOrWhiteSpace(note)
                    ? note
                    : $"Xuất kho vật tư #{itemModelId} SL {quantity} (legacy – không có lô)",
                CreatedAt              = now
            });
        }

        await _unitOfWork.SaveAsync();
    }

    public async Task AdjustInventoryAsync(
        int depotId,
        int itemModelId,
        int quantityChange,
        Guid performedBy,
        string reason,
        string? note,
        DateTime? expiredDate,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var inventory = await _unitOfWork.SetTracked<SupplyInventory>()
            .FirstOrDefaultAsync(x => x.DepotId == depotId && x.ItemModelId == itemModelId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Không tìm thấy tồn kho vật tư #{itemModelId} tại kho #{depotId}.");

        if (quantityChange < 0)
        {
            // ── Decrease: FEFO lot deduction ───────────────────────────────
            var decrease  = -quantityChange;
            var available = (inventory.Quantity ?? 0) - (inventory.MissionReservedQuantity + inventory.TransferReservedQuantity);
            if (available < decrease)
                throw new InvalidOperationException(
                    $"Vật tư #{itemModelId}: số lượng khả dụng ({available}) không đủ để điều chỉnh giảm {decrease}.");

            inventory.Quantity      = (inventory.Quantity ?? 0) - decrease;
            inventory.LastStockedAt = now;

            var lots = await _unitOfWork.SetTracked<SupplyInventoryLot>()
                .Where(l => l.SupplyInventoryId == inventory.Id && l.RemainingQuantity > 0)
                .OrderBy(l => l.ExpiredDate == null ? 1 : 0)
                .ThenBy(l => l.ExpiredDate)
                .ThenBy(l => l.ReceivedDate)
                .ToListAsync(cancellationToken);

            if (lots.Count > 0)
            {
                var remaining = decrease;
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
                        ActionType             = InventoryActionType.Adjust.ToString(),
                        QuantityChange         = -deduct,
                        SourceType             = InventorySourceType.Adjustment.ToString(),
                        PerformedBy            = performedBy,
                        Note                   = !string.IsNullOrWhiteSpace(note)
                            ? $"{note} [lô #{lot.Id}, SL -{deduct}]"
                            : $"Điều chỉnh giảm FEFO lô #{lot.Id} vật tư #{itemModelId} SL {deduct}: {reason}",
                        CreatedAt              = now
                    });
                }

                if (remaining > 0)
                    throw new InvalidOperationException(
                        $"Vật tư #{itemModelId}: không đủ lô để điều chỉnh giảm {decrease}.");
            }
            else
            {
                // Fallback: no lots (legacy data)
                await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
                {
                    DepotSupplyInventoryId = inventory.Id,
                    ActionType             = InventoryActionType.Adjust.ToString(),
                    QuantityChange         = quantityChange,
                    SourceType             = InventorySourceType.Adjustment.ToString(),
                    PerformedBy            = performedBy,
                    Note                   = !string.IsNullOrWhiteSpace(note)
                        ? note
                        : $"Điều chỉnh giảm vật tư #{itemModelId} SL {decrease}: {reason} (legacy – không có lô)",
                    CreatedAt              = now
                });
            }
        }
        else
        {
            // ── Increase: create new lot + increment quantity ───────────────
            var lot = new SupplyInventoryLot
            {
                SupplyInventoryId = inventory.Id,
                Quantity          = quantityChange,
                RemainingQuantity = quantityChange,
                ReceivedDate      = now,
                ExpiredDate       = expiredDate,
                SourceType        = InventorySourceType.Adjustment.ToString(),
                SourceId          = null,
                CreatedAt         = now
            };
            await _unitOfWork.GetRepository<SupplyInventoryLot>().AddAsync(lot);
            await _unitOfWork.SaveAsync(); // flush to get lot.Id

            inventory.Quantity      = (inventory.Quantity ?? 0) + quantityChange;
            inventory.LastStockedAt = now;

            await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
            {
                DepotSupplyInventoryId = inventory.Id,
                SupplyInventoryLotId   = lot.Id,
                ActionType             = InventoryActionType.Adjust.ToString(),
                QuantityChange         = quantityChange,
                SourceType             = InventorySourceType.Adjustment.ToString(),
                PerformedBy            = performedBy,
                Note                   = !string.IsNullOrWhiteSpace(note)
                    ? note
                    : $"Điều chỉnh tăng vật tư #{itemModelId} SL {quantityChange}: {reason}",
                CreatedAt              = now
            });
        }

        await _unitOfWork.SaveAsync();
    }

    public async Task<MissionSupplyReturnExecutionResult> ReceiveMissionReturnAsync(
        int depotId,
        int missionId,
        int activityId,
        Guid performedBy,
        List<(int ItemModelId, int Quantity)> consumableItems,
        List<(int ReusableItemId, string? Condition, string? Note)> reusableItems,
        List<(int ItemModelId, int Quantity)> legacyReusableQuantities,
        string? discrepancyNote,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var normalizedConsumables = consumableItems
            .Where(x => x.Quantity > 0)
            .GroupBy(x => x.ItemModelId)
            .Select(group => (ItemModelId: group.Key, Quantity: group.Sum(x => x.Quantity)))
            .ToList();
        var normalizedLegacyReusable = legacyReusableQuantities
            .Where(x => x.Quantity > 0)
            .GroupBy(x => x.ItemModelId)
            .Select(group => (ItemModelId: group.Key, Quantity: group.Sum(x => x.Quantity)))
            .ToList();
        var normalizedReusableItems = reusableItems
            .Where(x => x.ReusableItemId > 0)
            .GroupBy(x => x.ReusableItemId)
            .Select(g => g.First())
            .ToList();
        var normalizedReusableIds = normalizedReusableItems
            .Select(x => x.ReusableItemId)
            .ToList();
        var reusableUpdateLookup = normalizedReusableItems
            .ToDictionary(x => x.ReusableItemId);

        var itemIds = normalizedConsumables.Select(x => x.ItemModelId)
            .Concat(normalizedLegacyReusable.Select(x => x.ItemModelId))
            .Distinct()
            .ToList();
        var itemLookup = itemIds.Count == 0
            ? new Dictionary<int, ItemModel>()
            : await _unitOfWork.Set<ItemModel>()
                .Where(i => itemIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id, cancellationToken);

        var result = new MissionSupplyReturnExecutionResult
        {
            UsedLegacyFallback = normalizedLegacyReusable.Count > 0,
            DiscrepancyRecorded = !string.IsNullOrWhiteSpace(discrepancyNote)
        };
        var resultLookup = new Dictionary<int, MissionSupplyReturnExecutionItemDto>();

        foreach (var (itemModelId, quantity) in normalizedConsumables)
        {
            if (!itemLookup.TryGetValue(itemModelId, out var itemModel))
                throw new InvalidOperationException($"Không tìm thấy metadata vật tư #{itemModelId}.");

            var inventory = await _unitOfWork.SetTracked<SupplyInventory>()
                .FirstOrDefaultAsync(x => x.DepotId == depotId && x.ItemModelId == itemModelId, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Không tìm thấy tồn kho vật tư #{itemModelId} tại kho #{depotId} để nhập lại từ mission.");

            var lot = new SupplyInventoryLot
            {
                SupplyInventoryId = inventory.Id,
                Quantity = quantity,
                RemainingQuantity = quantity,
                ReceivedDate = now,
                ExpiredDate = null,
                SourceType = InventorySourceType.Mission.ToString(),
                SourceId = activityId,
                CreatedAt = now
            };
            await _unitOfWork.GetRepository<SupplyInventoryLot>().AddAsync(lot);
            await _unitOfWork.SaveAsync();

            inventory.Quantity = (inventory.Quantity ?? 0) + quantity;
            inventory.LastStockedAt = now;

            await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
            {
                DepotSupplyInventoryId = inventory.Id,
                SupplyInventoryLotId = lot.Id,
                ActionType = InventoryActionType.Return.ToString(),
                QuantityChange = quantity,
                SourceType = InventorySourceType.Mission.ToString(),
                SourceId = activityId,
                MissionId = missionId,
                PerformedBy = performedBy,
                Note = BuildMissionReturnNote(
                    $"Nhập lại {itemModel.Name} từ activity #{activityId}, mission #{missionId}",
                    discrepancyNote),
                CreatedAt = now
            });

            var resultItem = GetOrCreateReturnResultItem(resultLookup, itemModel);
            resultItem.ActualQuantity += quantity;
        }

        var explicitReusableUnits = normalizedReusableIds.Count == 0
            ? []
            : await _unitOfWork.SetTracked<ReusableItem>()
                .Where(r => normalizedReusableIds.Contains(r.Id))
                .Include(r => r.ItemModel)
                .ToListAsync(cancellationToken);

        if (explicitReusableUnits.Count != normalizedReusableIds.Count)
        {
            var foundIds = explicitReusableUnits.Select(x => x.Id).ToHashSet();
            var missingIds = normalizedReusableIds.Where(id => !foundIds.Contains(id));
            throw new InvalidOperationException($"Không tìm thấy reusable units: {string.Join(", ", missingIds)}.");
        }

        foreach (var unit in explicitReusableUnits)
        {
            if (!string.Equals(unit.Status, nameof(ReusableItemStatus.InUse), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Reusable unit #{unit.Id} không ở trạng thái InUse.");

            if (unit.DepotId != depotId)
                throw new InvalidOperationException($"Reusable unit #{unit.Id} không thuộc kho #{depotId}.");

            var itemModel = unit.ItemModel
                ?? throw new InvalidOperationException($"Reusable unit #{unit.Id} không có metadata vật tư.");

            unit.DepotId = depotId;
            unit.Status = nameof(ReusableItemStatus.Available);
            unit.SupplyRequestId = null;
            unit.UpdatedAt = now;

            if (reusableUpdateLookup.TryGetValue(unit.Id, out var updateInfo))
            {
                if (!string.IsNullOrWhiteSpace(updateInfo.Condition))
                    unit.Condition = updateInfo.Condition;
                if (updateInfo.Note != null)
                    unit.Note = updateInfo.Note;
            }

            await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
            {
                ReusableItemId = unit.Id,
                ActionType = InventoryActionType.Return.ToString(),
                QuantityChange = 1,
                SourceType = InventorySourceType.Mission.ToString(),
                SourceId = activityId,
                MissionId = missionId,
                PerformedBy = performedBy,
                Note = BuildMissionReturnNote(
                    $"Nhận lại {itemModel.Name} (S/N: {unit.SerialNumber ?? "N/A"}) từ activity #{activityId}, mission #{missionId}",
                    discrepancyNote),
                CreatedAt = now
            });

            var resultItem = GetOrCreateReturnResultItem(resultLookup, itemModel);
            resultItem.ActualQuantity += 1;
            resultItem.ReturnedReusableUnits.Add(new SupplyExecutionReusableUnitDto
            {
                ReusableItemId = unit.Id,
                ItemModelId = itemModel.Id,
                ItemName = itemModel.Name ?? string.Empty,
                SerialNumber = unit.SerialNumber,
                Condition = unit.Condition,
                Note = unit.Note
            });
        }

        foreach (var (itemModelId, quantity) in normalizedLegacyReusable)
        {
            if (!itemLookup.TryGetValue(itemModelId, out var itemModel))
                throw new InvalidOperationException($"Không tìm thấy metadata vật tư reusable #{itemModelId}.");

            var legacyUnits = await _unitOfWork.SetTracked<ReusableItem>()
                .Where(r => r.DepotId == depotId
                    && r.ItemModelId == itemModelId
                    && r.Status == nameof(ReusableItemStatus.InUse)
                    && r.SupplyRequestId == null
                    && !normalizedReusableIds.Contains(r.Id))
                .OrderBy(r => r.Id)
                .Take(quantity)
                .Include(r => r.ItemModel)
                .ToListAsync(cancellationToken);

            if (legacyUnits.Count != quantity)
                throw new InvalidOperationException(
                    $"Vật tư reusable #{itemModelId}: chỉ tìm thấy {legacyUnits.Count} đơn vị InUse để nhập lại theo legacy fallback, yêu cầu {quantity}.");

            foreach (var unit in legacyUnits)
            {
                unit.DepotId = depotId;
                unit.Status = nameof(ReusableItemStatus.Available);
                unit.SupplyRequestId = null;
                unit.UpdatedAt = now;

                await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
                {
                    ReusableItemId = unit.Id,
                    ActionType = InventoryActionType.Return.ToString(),
                    QuantityChange = 1,
                    SourceType = InventorySourceType.Mission.ToString(),
                    SourceId = activityId,
                    MissionId = missionId,
                    PerformedBy = performedBy,
                    Note = BuildMissionReturnNote(
                        $"Nhận lại {itemModel.Name} (S/N: {unit.SerialNumber ?? "N/A"}) theo legacy fallback từ activity #{activityId}, mission #{missionId}",
                        discrepancyNote),
                    CreatedAt = now
                });

                var resultItem = GetOrCreateReturnResultItem(resultLookup, itemModel);
                resultItem.ActualQuantity += 1;
                resultItem.ReturnedReusableUnits.Add(new SupplyExecutionReusableUnitDto
                {
                    ReusableItemId = unit.Id,
                    ItemModelId = itemModel.Id,
                    ItemName = itemModel.Name ?? string.Empty,
                    SerialNumber = unit.SerialNumber,
                    Condition = unit.Condition,
                    Note = unit.Note
                });
            }
        }

        await _unitOfWork.SaveAsync();

        result.Items = resultLookup.Values
            .OrderBy(x => x.ItemModelId)
            .ToList();
        return result;
    }

    private static string BuildMissionReturnNote(string baseNote, string? discrepancyNote)
    {
        if (string.IsNullOrWhiteSpace(discrepancyNote))
            return baseNote;

        return $"{baseNote}. Ghi chú chênh lệch: {discrepancyNote}";
    }

    private static MissionSupplyReturnExecutionItemDto GetOrCreateReturnResultItem(
        IDictionary<int, MissionSupplyReturnExecutionItemDto> lookup,
        ItemModel itemModel)
    {
        if (!lookup.TryGetValue(itemModel.Id, out var item))
        {
            item = new MissionSupplyReturnExecutionItemDto
            {
                ItemModelId = itemModel.Id,
                ItemName = itemModel.Name ?? string.Empty,
                Unit = itemModel.Unit
            };
            lookup[itemModel.Id] = item;
        }

        return item;
    }

    private async Task<List<SupplyExecutionLotDto>> BuildReservedLotPlanAsync(
        int supplyInventoryId,
        int alreadyReservedQuantity,
        int requestedQuantity,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var lots = await _unitOfWork.Set<SupplyInventoryLot>()
            .Where(l => l.SupplyInventoryId == supplyInventoryId
                && l.RemainingQuantity > 0
                && (!l.ExpiredDate.HasValue || l.ExpiredDate.Value >= now))
            .OrderBy(l => l.ExpiredDate == null ? 1 : 0)
            .ThenBy(l => l.ExpiredDate)
            .ThenBy(l => l.ReceivedDate)
            .ToListAsync(cancellationToken);

        if (lots.Count == 0)
            return [];

        var remainingReserved = Math.Max(0, alreadyReservedQuantity);
        var remainingRequest = requestedQuantity;
        var plannedLots = new List<SupplyExecutionLotDto>();

        foreach (var lot in lots)
        {
            if (remainingRequest <= 0)
                break;

            var lotAvailableForPlanning = lot.RemainingQuantity;
            if (remainingReserved > 0)
            {
                var reservedFromLot = Math.Min(lotAvailableForPlanning, remainingReserved);
                remainingReserved -= reservedFromLot;
                lotAvailableForPlanning -= reservedFromLot;
            }

            if (lotAvailableForPlanning <= 0)
                continue;

            var allocate = Math.Min(lotAvailableForPlanning, remainingRequest);
            remainingRequest -= allocate;

            plannedLots.Add(new SupplyExecutionLotDto
            {
                LotId = lot.Id,
                QuantityTaken = allocate,
                ReceivedDate = lot.ReceivedDate,
                ExpiredDate = lot.ExpiredDate,
                RemainingQuantityAfterExecution = lot.RemainingQuantity - allocate
            });
        }

        return plannedLots;
    }

    // ─── Depot Closure bulk operations ───────────────────────────────────────

    /// <summary>
    /// Cursor-based, resumable batch transfer of ALL consumable + reusable inventory
    /// from <paramref name="sourceDepotId"/> to <paramref name="targetDepotId"/>.
    /// Each call processes up to <paramref name="batchSize"/> supply_inventory rows.
    /// Returns (processedRows, lastInventoryId) so the handler can persist progress.
    /// Reusable items are re-homed in a single pass after consumables.
    /// </summary>
    public async Task<(int ProcessedRows, int? LastInventoryId)> BulkTransferForClosureAsync(
        int sourceDepotId,
        int targetDepotId,
        int closureId,
        Guid performedBy,
        int? lastProcessedInventoryId,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        int processedRows = 0;
        int? lastId = lastProcessedInventoryId;

        // ── 1. Consumable rows (supply_inventory) — cursor paginated ─────────
        var inventoryQuery = _unitOfWork.SetTracked<SupplyInventory>()
            .Where(inv => inv.DepotId == sourceDepotId && (inv.Quantity ?? 0) > 0);

        if (lastId.HasValue)
            inventoryQuery = inventoryQuery.Where(inv => inv.Id > lastId.Value);

        var batch = await inventoryQuery
            .OrderBy(inv => inv.Id)
            .Take(batchSize)
            .Include(inv => inv.Lots)
            .ToListAsync(cancellationToken);

        foreach (var srcInv in batch)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var itemModelId = srcInv.ItemModelId;
            var totalQty    = srcInv.Quantity ?? 0;
            if (totalQty <= 0) { lastId = srcInv.Id; processedRows++; continue; }

            // ── find or create destination SupplyInventory row ───────────────
            var dstInv = await _unitOfWork.SetTracked<SupplyInventory>()
                .FirstOrDefaultAsync(inv => inv.DepotId == targetDepotId && inv.ItemModelId == itemModelId, cancellationToken);

            if (dstInv == null)
            {
                dstInv = new SupplyInventory
                {
                    DepotId           = targetDepotId,
                    ItemModelId       = itemModelId,
                    Quantity          = 0,
                    MissionReservedQuantity  = 0,
                    TransferReservedQuantity = 0,
                    LastStockedAt     = now
                };
                await _unitOfWork.GetRepository<SupplyInventory>().AddAsync(dstInv);
                await _unitOfWork.SaveAsync(); // flush to get dstInv.Id
            }

            // ── transfer lots FEFO ───────────────────────────────────────────
            var lots = srcInv.Lots
                .Where(l => l.RemainingQuantity > 0)
                .OrderBy(l => l.ExpiredDate == null ? 1 : 0)
                .ThenBy(l => l.ExpiredDate)
                .ThenBy(l => l.ReceivedDate)
                .ToList();

            foreach (var srcLot in lots)
            {
                var qty = srcLot.RemainingQuantity;

                // source: zero out lot
                srcLot.RemainingQuantity = 0;

                // source: log outbound
                await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
                {
                    DepotSupplyInventoryId = srcInv.Id,
                    SupplyInventoryLotId   = srcLot.Id,
                    ActionType             = InventoryActionType.TransferOut.ToString(),
                    QuantityChange         = -qty,
                    SourceType             = "DepotClosure",
                    SourceId               = closureId,
                    PerformedBy            = performedBy,
                    Note                   = $"Đóng kho #{sourceDepotId}: chuyển lô #{srcLot.Id} vật tư #{itemModelId} SL {qty} sang kho #{targetDepotId}",
                    ExpiredDate            = srcLot.ExpiredDate,
                    ReceivedDate           = srcLot.ReceivedDate,
                    CreatedAt              = now
                });

                // destination: create new lot
                var dstLot = new SupplyInventoryLot
                {
                    SupplyInventoryId = dstInv.Id,
                    Quantity          = qty,
                    RemainingQuantity = qty,
                    ReceivedDate      = srcLot.ReceivedDate ?? now,
                    ExpiredDate       = srcLot.ExpiredDate,
                    SourceType        = "DepotClosure",
                    SourceId          = closureId,
                    CreatedAt         = now
                };
                await _unitOfWork.GetRepository<SupplyInventoryLot>().AddAsync(dstLot);
                await _unitOfWork.SaveAsync(); // flush to get dstLot.Id

                // destination: log inbound
                await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
                {
                    DepotSupplyInventoryId = dstInv.Id,
                    SupplyInventoryLotId   = dstLot.Id,
                    ActionType             = InventoryActionType.TransferIn.ToString(),
                    QuantityChange         = qty,
                    SourceType             = "DepotClosure",
                    SourceId               = closureId,
                    PerformedBy            = performedBy,
                    Note                   = $"Đóng kho #{sourceDepotId}: nhận lô từ kho nguồn, vật tư #{itemModelId} SL {qty}",
                    ExpiredDate            = srcLot.ExpiredDate,
                    ReceivedDate           = srcLot.ReceivedDate,
                    CreatedAt              = now
                });

                dstInv.Quantity      = (dstInv.Quantity ?? 0) + qty;
                dstInv.LastStockedAt = now;
            }

            // zero out source inventory
            srcInv.Quantity                = 0;
            srcInv.MissionReservedQuantity = 0;
            srcInv.TransferReservedQuantity = 0;
            srcInv.LastStockedAt            = now;
            srcInv.IsDeleted                = true;

            lastId = srcInv.Id;
            processedRows++;
        }

        // ── 2. Reusable items — move Available units to target depot ─────────
        // Only run when consumable batch is exhausted (batch.Count < batchSize)
        // so caller knows it's the final pass.
        if (batch.Count < batchSize)
        {
            var reusableItems = await _unitOfWork.GetRepository<ReusableItem>()
                .AsQueryable(tracked: true)
                .Where(r => r.DepotId == sourceDepotId && r.Status == nameof(ReusableItemStatus.Available))
                .ToListAsync(cancellationToken);

            foreach (var unit in reusableItems)
            {
                var oldDepotId = unit.DepotId;
                unit.DepotId    = targetDepotId;
                unit.UpdatedAt  = now;

                await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
                {
                    ReusableItemId = unit.Id,
                    ActionType     = InventoryActionType.TransferOut.ToString(),
                    QuantityChange = 1,
                    SourceType     = "DepotClosure",
                    SourceId       = closureId,
                    PerformedBy    = performedBy,
                    Note           = $"Đóng kho #{oldDepotId}: di chuyển thiết bị #{unit.Id} sang kho #{targetDepotId}",
                    CreatedAt      = now
                });
            }
        }

        await _unitOfWork.SaveAsync();
        return (processedRows, lastId);
    }

    /// <summary>
    /// Zero out ALL consumable inventory and decommission Available reusable items
    /// for a depot being closed via external resolution.
    /// Creates per-lot inventory logs with SourceType = "DepotClosure".
    /// </summary>
    public async Task ZeroOutForClosureAsync(
        int depotId,
        int closureId,
        Guid performedBy,
        string? note,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // ── 1. Consumables ────────────────────────────────────────────────────
        var inventories = await _unitOfWork.GetRepository<SupplyInventory>()
            .AsQueryable(tracked: true)
            .Where(inv => inv.DepotId == depotId && (inv.Quantity ?? 0) > 0)
            .Include(inv => inv.Lots)
            .ToListAsync(cancellationToken);

        foreach (var inv in inventories)
        {
            var lots = inv.Lots
                .Where(l => l.RemainingQuantity > 0)
                .OrderBy(l => l.ExpiredDate == null ? 1 : 0)
                .ThenBy(l => l.ExpiredDate)
                .ThenBy(l => l.ReceivedDate)
                .ToList();

            foreach (var lot in lots)
            {
                var qty = lot.RemainingQuantity;
                lot.RemainingQuantity = 0;

                await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
                {
                    DepotSupplyInventoryId = inv.Id,
                    SupplyInventoryLotId   = lot.Id,
                    ActionType             = "DepotClosureExternalDisposal",
                    QuantityChange         = -qty,
                    SourceType             = "DepotClosure",
                    SourceId               = closureId,
                    PerformedBy            = performedBy,
                    Note                   = $"Đóng kho #{depotId} (xử lý bên ngoài): xuất lô #{lot.Id} vật tư #{inv.ItemModelId} SL {qty}. {note}",
                    ExpiredDate            = lot.ExpiredDate,
                    ReceivedDate           = lot.ReceivedDate,
                    CreatedAt              = now
                });
            }

            inv.Quantity                 = 0;
            inv.MissionReservedQuantity  = 0;
            inv.TransferReservedQuantity = 0;
            inv.LastStockedAt            = now;
            inv.IsDeleted                = true;
        }

        // ── 2. Reusable items — decommission Available units ──────────────────
        var reusableItems = await _unitOfWork.GetRepository<ReusableItem>()
            .AsQueryable(tracked: true)
            .Where(r => r.DepotId == depotId && r.Status == nameof(ReusableItemStatus.Available))
            .ToListAsync(cancellationToken);

        foreach (var unit in reusableItems)
        {
            unit.Status    = nameof(ReusableItemStatus.Decommissioned);
            unit.UpdatedAt = now;
            unit.Note      = $"Đóng kho #{depotId} (xử lý bên ngoài) — closureId #{closureId}. {note}";
            unit.IsDeleted = true;

            await _unitOfWork.GetRepository<InventoryLog>().AddAsync(new InventoryLog
            {
                ReusableItemId = unit.Id,
                ActionType     = "DepotClosureReusableDecommissioned",
                QuantityChange = -1,
                SourceType     = "DepotClosure",
                SourceId       = closureId,
                PerformedBy    = performedBy,
                Note           = $"Đóng kho #{depotId} (xử lý bên ngoài): thanh lý thiết bị #{unit.Id}. {note}",
                CreatedAt      = now
            });
        }

        await _unitOfWork.SaveAsync();
    }

    public async Task<bool> HasActiveInventoryCommitmentsAsync(int depotId, CancellationToken cancellationToken = default)
    {
        // Consumable: any item reserved for an active mission
        var hasMissionReservation = await _unitOfWork.Set<SupplyInventory>()
            .AnyAsync(inv => inv.DepotId == depotId && inv.MissionReservedQuantity > 0, cancellationToken);

        if (hasMissionReservation) return true;

        // Reusable: any unit currently in active mission use
        var hasReusableInUse = await _unitOfWork.Set<ReusableItem>()
            .AnyAsync(ri => ri.DepotId == depotId && ri.Status == "InUse", cancellationToken);

        return hasReusableInUse;
    }
}


