namespace RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure;

public class InitiateDepotClosureResponse
{
    /// <summary>ID của bản ghi đóng kho — dùng để gọi Phase 2 (Resolve).</summary>
    public int ClosureId { get; set; }

    public int DepotId { get; set; }
    public string DepotName { get; set; } = string.Empty;

    /// <summary>
    /// true = còn hàng, frontend phải hiện form chọn cách xử lý (chuyển kho / xử lý ngoài).
    /// false = kho trống, đã đóng ngay lập tức — không cần Phase 2.
    /// </summary>
    public bool RequiresResolution { get; set; }

    /// <summary>
    /// Trạng thái hiện tại của bản ghi đóng kho.
    /// Frontend dùng field này để quyết định UI:
    ///   "InProgress"      → Đang chờ admin chọn option → hiển thị countdown + form lựa chọn.
    ///   "Processing"      → Server đang xử lý lựa chọn → hiển thị loading.
    ///   "TransferPending" → Đã chọn chuyển kho, đang chờ transfer hoàn tất → KHÔNG hiển thị countdown.
    ///   "Completed"       → Đóng kho thành công.
    ///   "Cancelled"       → Đã huỷ.
    ///   "TimedOut"        → Hết thời gian — kho đã khôi phục.
    /// </summary>
    public string ClosureStatus { get; set; } = string.Empty;

    /// <summary>Tổng quan tồn kho tại thời điểm initiate.</summary>
    public InventorySummaryDto InventorySummary { get; set; } = new();

    /// <summary>Thời hạn admin phải hoàn thành Phase 2. Sau thời gian này kho tự khôi phục.</summary>
    public DateTime? TimeoutAt { get; set; }

    public string Message { get; set; } = string.Empty;
}

public class InventorySummaryDto
{
    /// <summary>Số loại vật tư tiêu hao còn trong kho.</summary>
    public int ConsumableItemTypeCount { get; set; }

    /// <summary>Tổng số đơn vị vật tư tiêu hao.</summary>
    public int ConsumableUnitTotal { get; set; }

    /// <summary>Số thiết bị tái sử dụng đang ở trong kho (Available).</summary>
    public int ReusableAvailableCount { get; set; }

    /// <summary>Số thiết bị tái sử dụng đang được dùng trong nhiệm vụ (InUse) — không xử lý được ngay.</summary>
    public int ReusableInUseCount { get; set; }
}
