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
