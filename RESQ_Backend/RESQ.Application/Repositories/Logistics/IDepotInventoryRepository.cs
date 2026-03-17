using RESQ.Application.Common.Models;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryByCategory;
using RESQ.Application.UseCases.Logistics.Queries.SearchWarehousesByItems;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.Repositories.Logistics;

public interface IDepotInventoryRepository
{
    Task<int?> GetActiveDepotIdByManagerAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<List<int>> GetActiveDepotIdsByManagerAsync(Guid userId, CancellationToken cancellationToken = default);
    
    Task<PagedResult<InventoryItemModel>> GetInventoryPagedAsync(
        int depotId,
        List<int>? categoryIds,
        List<ItemType>? itemTypes,
        List<TargetGroup>? targetGroups,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy tổng số lượng tồn kho theo danh mục của một kho cụ thể.
    /// </summary>
    Task<List<DepotCategoryQuantityDto>> GetInventoryByCategoryAsync(int depotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tìm kiếm vật tư theo từ khoá danh mục/loại để agent AI dùng trong quá trình lập kế hoạch.
    /// </summary>
    Task<(List<AgentInventoryItem> Items, int TotalCount)> SearchForAgentAsync(
        string categoryKeyword,
        string? typeKeyword,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the lat/lng of a depot by its ID, or null if not found / no location set.
    /// </summary>
    Task<(double Latitude, double Longitude)?> GetDepotLocationAsync(int depotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tìm kiếm các kho cứu trợ có chứa vật tư theo danh sách mã vật tư.
    /// Chỉ trả về kho có số lượng khả dụng >= quantity.
    /// Trả về danh sách phẳng (item, depot) để handler nhóm lại.
    /// </summary>
    Task<(List<WarehouseItemRow> Rows, int TotalItemCount)> SearchWarehousesByItemsAsync(
        List<int>? itemModelIds,
        Dictionary<int, int> itemQuantities,
        bool activeDepotsOnly,
        int? excludeDepotId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra tồn kho tại một kho cụ thể cho danh sách vật tư.
    /// Trả về danh sách vật tư không đủ số lượng hoặc không có trong kho.
    /// Số lượng khả dụng = Quantity - ReservedQuantity.
    /// </summary>
    Task<List<SupplyShortageResult>> CheckSupplyAvailabilityAsync(
        int depotId,
        List<(int ReliefItemId, string ItemName, int RequestedQuantity)> items,
        CancellationToken cancellationToken = default);
}
