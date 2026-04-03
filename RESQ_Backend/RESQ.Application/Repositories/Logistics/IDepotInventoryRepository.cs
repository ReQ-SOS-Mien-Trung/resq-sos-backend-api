using RESQ.Application.Common.Models;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryByCategory;
using RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems;
using RESQ.Application.UseCases.Logistics.Queries.SearchWarehousesByItems;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Entities.Logistics.Models;
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
    /// Lấy danh sách các lô hàng (lot) của một mặt hàng tại kho, sắp xếp theo FEFO.
    /// </summary>
    Task<PagedResult<InventoryLotModel>> GetInventoryLotsAsync(
        int depotId, int itemModelId, int pageNumber, int pageSize,
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
        List<(int ItemModelId, string ItemName, int RequestedQuantity)> items,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Đặt trước vật tư cho mission: tăng ReservedQuantity.
    /// Gọi ngay sau khi mission được tạo thành công.
    /// </summary>
    Task ReserveSuppliesAsync(
        int depotId,
        List<(int ItemModelId, int Quantity)> items,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Team xác nhận đã lấy hàng: giảm cả Quantity và ReservedQuantity, ghi InventoryLog.
    /// </summary>
    Task ConsumeReservedSuppliesAsync(
        int depotId,
        List<(int ItemModelId, int Quantity)> items,
        Guid performedBy,
        int activityId,
        int missionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy dữ liệu thô vật tư tiêu hao để application/domain tự resolve threshold và phân loại mức tồn.
    /// </summary>
    Task<List<LowStockRawItemDto>> GetLowStockRawItemsAsync(
        int? depotId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Giải phóng vật tư đã đặt trước (ví dụ khi huỷ activity hoặc thay đổi items): giảm ReservedQuantity.
    /// </summary>
    Task ReleaseReservedSuppliesAsync(
        int depotId,
        List<(int ItemModelId, int Quantity)> items,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xuất kho thủ công (Export): giảm Quantity, áp dụng FEFO trên các lô và ghi InventoryLog.
    /// Chỉ được xuất số lượng khả dụng (Quantity - ReservedQuantity).
    /// </summary>
    Task ExportInventoryAsync(
        int depotId,
        int itemModelId,
        int quantity,
        Guid performedBy,
        string? note,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Điều chỉnh tồn kho (Adjust): quantityChange dương → tạo lô mới + tăng Quantity;
    /// quantityChange âm → FEFO deduction trên các lô + giảm Quantity.
    /// </summary>
    Task AdjustInventoryAsync(
        int depotId,
        int itemModelId,
        int quantityChange,
        Guid performedBy,
        string reason,
        string? note,
        DateTime? expiredDate,
        CancellationToken cancellationToken = default);

    // ── Depot Closure helpers ────────────────────────────────────────────────

    /// <summary>
    /// Chuyển toàn bộ inventory từ kho đóng sang kho đích.
    /// Consumable: upsert supply_inventory tại đích + chuyển tất cả lots + ghi log.
    /// Reusable (Available): cập nhật depot_id sang kho đích + ghi log.
    /// Reusable (InUse): đánh dấu pending_reassignment, không di chuyển ngay.
    /// Xử lý theo cursor-based batch để có thể resume khi retry.
    /// Returns (processedRows, lastInventoryId) để handler lưu tiến trình.
    /// </summary>
    Task<(int ProcessedRows, int? LastInventoryId)> BulkTransferForClosureAsync(
        int sourceDepotId,
        int targetDepotId,
        int closureId,
        Guid performedBy,
        int? lastProcessedInventoryId = null,
        int batchSize = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tra cứu userId của quản lý đang được phân công tại kho (ngược với GetActiveDepotIdByManagerAsync).
    /// </summary>
    Task<Guid?> GetActiveManagerUserIdByDepotIdAsync(int depotId, CancellationToken ct = default);

    /// <summary>
    /// Zero-out toàn bộ inventory khi đóng kho theo hình thức xử lý bên ngoài.
    /// Ghi log đầy đủ từng lô hàng với closure_id để audit.
    /// Reusable (Available): chuyển sang Decommissioned.
    /// Reusable (InUse): đánh dấu pending_reassignment.
    /// </summary>
    Task ZeroOutForClosureAsync(
        int depotId,
        int closureId,
        Guid performedBy,
        string? note,
        CancellationToken cancellationToken = default);
}
