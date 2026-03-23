namespace RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems;

/// <summary>
/// Một dòng vật tư tồn kho đang ở mức cảnh báo hoặc nguy hiểm.
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

    /// <summary>Tỉ lệ khả dụng so với tổng (0.00 – 1.00).</summary>
    public double AvailableRatio { get; set; }

    /// <summary>Mức cảnh báo: "Danger" hoặc "Warning".</summary>
    public string AlertLevel { get; set; } = string.Empty;

    /// <summary>Nhãn tiếng Việt của mức cảnh báo.</summary>
    public string AlertLevelLabel { get; set; } = string.Empty;
}
