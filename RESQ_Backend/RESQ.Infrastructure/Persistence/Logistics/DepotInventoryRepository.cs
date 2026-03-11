using System.Linq.Expressions;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services.Logistics;
using RESQ.Domain.Entities.Logistics;
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
}
