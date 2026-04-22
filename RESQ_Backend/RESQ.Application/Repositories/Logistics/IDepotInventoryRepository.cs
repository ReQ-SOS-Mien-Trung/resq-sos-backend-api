using RESQ.Application.Common.Models;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryByCategory;
using RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems;
using RESQ.Application.UseCases.Logistics.Queries.GetMyDepotItemModelAlerts;
using RESQ.Application.UseCases.Logistics.Queries.GetMyDepotReusableUnits;
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
        string? itemName,
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
    /// Tìm kiếm vật phẩm theo từ khoá danh mục/loại để agent AI dùng trong quá trình lập kế hoạch.
    /// Trả về cả Consumable lẫn Reusable; với Reusable, AvailableQuantity là số đơn vị Available.
    /// </summary>
    Task<(List<AgentInventoryItem> Items, int TotalCount)> SearchForAgentAsync(
        string categoryKeyword,
        string? typeKeyword,
        int page,
        int pageSize,
        IReadOnlyCollection<int>? allowedDepotIds = null,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the lat/lng of a depot by its ID, or null if not found / no location set.
    /// </summary>
    Task<(double Latitude, double Longitude)?> GetDepotLocationAsync(int depotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tìm kiếm các kho cứu trợ có chứa vật phẩm theo danh sách mã vật phẩm.
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
    /// Kiểm tra tồn kho tại một kho cụ thể cho danh sách vật phẩm.
    /// Trả về danh sách vật phẩm không đủ số lượng hoặc không có trong kho.
    /// Số lượng khả dụng = Quantity - ReservedQuantity.
    /// </summary>
    Task<List<SupplyShortageResult>> CheckSupplyAvailabilityAsync(
        int depotId,
        List<(int ItemModelId, string ItemName, int RequestedQuantity)> items,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Đặt trước vật phẩm cho mission: tăng ReservedQuantity và trả về snapshot
    /// các lô FEFO hoặc reusable units activity cần lấy.
    /// </summary>
    Task<MissionSupplyReservationResult> ReserveSuppliesAsync(
        int depotId,
        List<(int ItemModelId, int Quantity)> items,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lập snapshot dự kiến cho mission suggestion mà không giữ chỗ hoặc ghi DB.
    /// Consumable dùng FEFO như reserve thật; reusable chọn các unit Available theo thứ tự ổn định.
    /// </summary>
    Task<MissionSupplyReservationResult> PreviewReserveSuppliesAsync(
        int depotId,
        List<(int ItemModelId, int Quantity)> items,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new MissionSupplyReservationResult());
    }

    /// <summary>
    /// Team xác nhận đã lấy hàng: giảm cả Quantity và ReservedQuantity, ghi InventoryLog,
    /// đồng thời trả về chi tiết thực tế đã lấy theo lot FEFO hoặc reusable unit.
    /// </summary>
    Task<MissionSupplyPickupExecutionResult> ConsumeReservedSuppliesAsync(
        int depotId,
        List<(int ItemModelId, int Quantity)> items,
        Guid performedBy,
        int activityId,
        int missionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Depot manager xác nhận nhận lại vật phẩm từ mission và nhập kho theo dữ liệu thực tế.
    /// Consumable được nhập lại theo quantity; Reusable được nhận lại theo từng unit id,
    /// hoặc quantity fallback cho legacy mission chưa có unit snapshot.
    /// Condition và Note của từng reusable unit sẽ được cập nhật nếu được cung cấp.
    /// </summary>
    Task<MissionSupplyReturnExecutionResult> ReceiveMissionReturnAsync(
        int depotId,
        int missionId,
        int activityId,
        Guid performedBy,
        List<(int ItemModelId, int Quantity, DateTime? ExpiredDate)> consumableItems,
        List<(int ReusableItemId, string? Condition, string? Note)> reusableItems,
        List<(int ItemModelId, int Quantity)> legacyReusableQuantities,
        string? discrepancyNote,
        CancellationToken cancellationToken = default);

    Task<MissionSupplyReturnExecutionResult> ReceiveMissionReturnByLotAsync(
        int depotId,
        int missionId,
        int activityId,
        Guid performedBy,
        List<(int ItemModelId, int Quantity, DateTime? ExpiredDate, int? SupplyInventoryLotId)> consumableItems,
        List<(int ReusableItemId, string? Condition, string? Note)> reusableItems,
        List<(int ItemModelId, int Quantity)> legacyReusableQuantities,
        string? discrepancyNote,
        CancellationToken cancellationToken = default)
    {
        return ReceiveMissionReturnAsync(
            depotId,
            missionId,
            activityId,
            performedBy,
            consumableItems.Select(item => (item.ItemModelId, item.Quantity, item.ExpiredDate)).ToList(),
            reusableItems,
            legacyReusableQuantities,
            discrepancyNote,
            cancellationToken);
    }

    /// <summary>
    /// Lấy dữ liệu thô vật phẩm tiêu hao để application/domain tự resolve threshold và phân loại mức tồn.
    /// </summary>
    Task<List<LowStockRawItemDto>> GetLowStockRawItemsAsync(
        int? depotId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy dữ liệu thô vật phẩm tiêu hao theo depot và danh mục.
    /// categoryIds hỗ trợ lọc OR: vật phẩm thuộc một trong các danh mục truyền vào đều được lấy.
    /// Default implementation fallback về hàm cũ để không phá test doubles hiện có.
    /// </summary>
    Task<List<LowStockRawItemDto>> GetLowStockRawItemsAsync(
        int? depotId,
        List<int>? categoryIds,
        CancellationToken cancellationToken = default)
        => GetLowStockRawItemsAsync(depotId, cancellationToken);

    /// <summary>
    /// Giải phóng vật phẩm đã đặt trước (ví dụ khi huỷ activity hoặc thay đổi items): giảm ReservedQuantity.
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

    // -- Depot Closure helpers ------------------------------------------------

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

    Task TransferClosureItemsAsync(
        int sourceDepotId,
        int targetDepotId,
        int closureId,
        int transferId,
        Guid performedBy,
        IReadOnlyCollection<DepotClosureTransferItemMoveDto> items,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gọi khi kho nguồn xác nhận xuất hàng (Ship step):
    /// - Consumable: cộng TransferReservedQuantity (giữ chỗ, giảm số lượng khả dụng hiển thị).
    /// - Reusable: chuyển N đơn vị từ Available → InTransit, DepotId = null, ghi log TransferOut.
    /// </summary>
    Task ReserveForClosureShipmentAsync(
        int sourceDepotId,
        int transferId,
        int closureId,
        Guid performedBy,
        IReadOnlyCollection<DepotClosureTransferItemMoveDto> items,
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

    /// <summary>
    /// Kiểm tra xem kho có đang có cam kết tồn kho chưa hoàn tất không:
    /// - Consumable: mission_reserved_quantity > 0 (đang được đặt cho nhiệm vụ cứu hộ).
    /// - Reusable: status = 'InUse' tại kho này (đang được sử dụng trong nhiệm vụ).
    /// Dùng để chặn chuyển sang Unavailable khi còn hoạt động đang diễn ra.
    /// </summary>
    Task<bool> HasActiveInventoryCommitmentsAsync(int depotId, CancellationToken cancellationToken = default);

    Task<DepotClosingBlockersModel> GetDepotClosingBlockersAsync(
        int depotId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new DepotClosingBlockersModel());

    // -- Dispose / Decommission helpers ----------------------------------------

    /// <summary>
    /// Xử lý (dispose) đồ tiêu hao theo lô cụ thể: giảm RemainingQuantity trong lô,
    /// giảm TotalQuantity trong SupplyInventory, ghi InventoryLog với ActionType=Adjust
    /// và SourceType=Expired hoặc Damaged.
    /// </summary>
    Task DisposeConsumableLotAsync(
        int depotId,
        int lotId,
        int quantity,
        string reason,
        string? note,
        Guid performedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ngừng sử dụng (decommission) thiết bị tái sử dụng: đặt Status=Decommissioned,
    /// ghi InventoryLog với ActionType=Adjust và SourceType=Damaged.
    /// Không cho phép decommission khi Status=InUse hoặc đã Decommissioned.
    /// </summary>
    Task DecommissionReusableItemAsync(
        int depotId,
        int reusableItemId,
        string? note,
        Guid performedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách lô hàng sắp hết hạn (ExpiredDate ≤ now + daysAhead) và còn hàng (RemainingQuantity > 0)
    /// tại một kho cụ thể.
    /// </summary>
    Task<List<ExpiringLotModel>> GetExpiringLotsAsync(
        int depotId,
        int daysAhead,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách đơn vị vật phẩm tái sử dụng tại kho với các bộ lọc:
    /// serial number, trạng thái (status), tình trạng (condition) và loại vật phẩm (itemModelId).
    /// </summary>
    Task<PagedResult<ReusableUnitDto>> GetReusableUnitsPagedAsync(
        int depotId,
        int? itemModelId,
        string? serialNumber,
        List<string>? statuses,
        List<string>? conditions,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int?> GetReusableItemDepotIdAsync(
        int reusableItemId,
        CancellationToken cancellationToken = default)
        => Task.FromException<int?>(new NotSupportedException("Repository này chưa hỗ trợ tra cứu depot của reusable item."));

    Task MarkReusableItemMaintenanceAsync(
        int depotId,
        int reusableItemId,
        string? note,
        Guid performedBy,
        CancellationToken cancellationToken = default)
        => Task.FromException(new NotSupportedException("Repository này chưa hỗ trợ chuyển reusable item sang Maintenance."));

    Task MarkReusableItemAvailableAsync(
        int depotId,
        int reusableItemId,
        ReusableItemCondition condition,
        string? note,
        Guid performedBy,
        CancellationToken cancellationToken = default)
        => Task.FromException(new NotSupportedException("Repository này chưa hỗ trợ chuyển reusable item về Available."));

    Task<List<ExpiringItemModelAlertRawDto>> GetExpiringItemModelAlertCandidatesAsync(
        int depotId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new List<ExpiringItemModelAlertRawDto>());

    Task<List<MaintenanceItemModelAlertRawDto>> GetMaintenanceItemModelAlertCandidatesAsync(
        int depotId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new List<MaintenanceItemModelAlertRawDto>());
}

public class DepotClosureTransferItemMoveDto
{
    public int ItemModelId { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
