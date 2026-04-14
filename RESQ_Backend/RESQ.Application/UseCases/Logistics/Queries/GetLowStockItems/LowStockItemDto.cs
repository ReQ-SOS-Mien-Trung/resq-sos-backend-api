namespace RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems;

/// <summary>
/// M?t dòng v?t ph?m t?n kho dang ? m?c c?nh báo.
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

    /// <summary>T?ng t?n kho.</summary>
    public int Quantity { get; set; }

    /// <summary>S? lu?ng dang du?c d?t gi?.</summary>
    public int ReservedQuantity { get; set; }

    /// <summary>S? lu?ng có th? dùng (Quantity - ReservedQuantity).</summary>
    public int AvailableQuantity { get; set; }

    /// <summary>Ngu?ng t?i thi?u dã du?c resolve (null n?u chua c?u hình).</summary>
    public int? MinimumThreshold { get; set; }

    /// <summary>severityRatio = max(0, available / minimumThreshold). 0 n?u chua c?u hình.</summary>
    public decimal SeverityRatio { get; set; }

    /// <summary>M?c c?nh báo: OK / LOW / MEDIUM / CRITICAL / UNCONFIGURED.</summary>
    public string WarningLevel { get; set; } = string.Empty;

    /// <summary>Scope mà threshold du?c resolve t?: Item / Category / Depot / Global / None.</summary>
    public string ResolvedThresholdScope { get; set; } = string.Empty;

    /// <summary>
    /// True khi không có config riêng cho item/category/depot - dang dùng ngu?ng m?c d?nh toàn h? th?ng.
    /// FE có th? dùng d? hi?n th? badge "Dùng ngu?ng m?c d?nh" ho?c l?c danh sách chua c?u hình riêng.
    /// </summary>
    public bool IsUsingGlobalDefault { get; set; }
}

