namespace RESQ.Domain.Entities.Logistics.Models;

/// <summary>
/// Flat model đại diện cho một dòng biến động kho dùng để xuất Excel.
/// </summary>
public class InventoryMovementRow
{
    public int RowNumber { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    /// <summary>Nhóm đối tượng, nhiều giá trị phân cách bởi dấu phẩy (dành cho Excel export).</summary>
    public string TargetGroup { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal? UnitPrice { get; set; }

    /// <summary>Số lượng thô từ DB (dương = nhập, âm = xuất).</summary>
    public int QuantityChange { get; set; }

    /// <summary>
    /// Số lượng được định dạng có dấu hiển thị:
    /// +N nếu hành động nhập/nhận; -N nếu hành động xuất.
    /// </summary>
    public string FormattedQuantity { get; set; } = string.Empty;

    public DateTime? CreatedAt { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string? MissionName { get; set; }

    /// <summary>Serial number cho đồ tái sử dụng (Reusable). Null với hàng tiêu thụ.</summary>
    public string? SerialNumber { get; set; }

    /// <summary>Lot ID cho hàng tiêu thụ (Consumable). Null với đồ tái sử dụng.</summary>
    public int? LotId { get; set; }
}
