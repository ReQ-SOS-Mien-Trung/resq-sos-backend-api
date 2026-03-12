using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Application.Services.Logistics;
using RESQ.Domain.Entities.Logistics;
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

    public async Task<PagedResult<InventoryItemModel>> GetInventoryPagedAsync(
        int depotId, 
        List<int>? categoryIds, 
        List<ItemType>? itemTypes, 
        List<TargetGroup>? targetGroups, 
        int pageNumber, 
        int pageSize, 
        CancellationToken cancellationToken = default)
    {
        // Safely map optional lists locally before passing to the Expression Tree to prevent NullReferenceExceptions
        var safeCategoryIds = categoryIds ?? new List<int>();
        var hasCategoryFilter = safeCategoryIds.Count > 0;

        var itemTypeStrings = itemTypes?.Select(e => e.ToString()).ToList() ?? new List<string>();
        var hasItemTypeFilter = itemTypeStrings.Count > 0;

        var targetGroupStrings = targetGroups?.Select(e => e.ToString().ToLower()).ToList() ?? new List<string>();
        var hasTargetGroupFilter = targetGroupStrings.Count > 0;

        // Build generic filter expression safely evaluating parameters and handling nullables
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
            includeProperties: "ReliefItem" // Removed invalid ReliefItem.Category include
        );

        // Fetch categories separately via generic repository to map category names 
        // (resolving the missing navigation property error while strictly using IUnitOfWork)
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
            ReliefItemId = x.ReliefItemId ?? 0, // Solves CS0266 (int? to int mismatch)
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
}
