namespace RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems;

/// <summary>
/// Một dòng vật phẩm tồn kho đang ở mức cảnh báo.
/// </summary>
public class LowStockItemDto
{
    public int DepotId { get; set; }
    public string DepotName { get; set; } = string.Empty;

    public int ItemModelId { get; set; }
    public string ItemModelName { get; set; } = string.Empty;
    public string? Unit { get; set; }

    public int? CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;

    public string? TargetGroup { get; set; }

    /// <summary>Tổng tồn kho.</summary>
    public int Quantity { get; set; }

    /// <summary>Số lượng đang được đặt giữ.</summary>
    public int ReservedQuantity { get; set; }

    /// <summary>Số lượng có thể dùng (Quantity - ReservedQuantity).</summary>
    public int AvailableQuantity { get; set; }

    /// <summary>Ngưỡng tối thiểu đã được resolve (null nếu chưa cấu hình).</summary>
    public int? MinimumThreshold { get; set; }

    /// <summary>severityRatio = max(0, available / minimumThreshold). 0 nếu chưa cấu hình.</summary>
    public decimal SeverityRatio { get; set; }

    /// <summary>Mức cảnh báo: OK / LOW / MEDIUM / CRITICAL / UNCONFIGURED.</summary>
    public string WarningLevel { get; set; } = string.Empty;

    /// <summary>Scope mà threshold được resolve từ: Item / Category / Depot / Global / None.</summary>
    public string ResolvedThresholdScope { get; set; } = string.Empty;

    /// <summary>
    /// True khi không có config riêng cho item/category/depot - đang dùng ngưỡng mặc định toàn hệ thống.
    /// FE có thể dùng để hiển thị badge "Dùng ngưỡng mặc định" hoặc lọc danh sách chưa cấu hình riêng.
    /// </summary>
    public bool IsUsingGlobalDefault { get; set; }
}

