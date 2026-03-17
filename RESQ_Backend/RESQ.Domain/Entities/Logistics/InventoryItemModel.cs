using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Domain.Entities.Logistics;

public class InventoryItemModel
{
    public int ItemModelId { get; set; }
    public string ItemModelName { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? ItemType { get; set; }
    public string? TargetGroup { get; set; }
    public InventoryAvailability Availability { get; set; } = default!;
    public DateTime? LastStockedAt { get; set; }

    /// <summary>
    /// Thống kê chi tiết trạng thái và tình trạng của vật phẩm tái sử dụng.
    /// Chỉ có giá trị khi ItemType == "Reusable"; null với tiêu hao.
    /// </summary>
    public ReusableBreakdown? ReusableBreakdown { get; set; }
}
