using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Domain.Entities.Logistics;

public class InventoryItemModel
{
    public int ItemModelId { get; set; }
    public string ItemModelName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int? CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? ItemType { get; set; }
    public decimal? WeightPerUnit { get; set; }
    public decimal? VolumePerUnit { get; set; }
    public List<string> TargetGroups { get; set; } = new();
    public InventoryAvailability Availability { get; set; } = default!;
    public DateTime? LastStockedAt { get; set; }

    /// <summary>
    /// Thống kê chi tiết trạng thái và tình trạng của vật phẩm tái sử dụng.
    /// Chỉ có giá trị khi ItemType == "Reusable"; null với tiêu hao.
    /// </summary>
    public ReusableBreakdown? ReusableBreakdown { get; set; }

    /// <summary>
    /// Ngày hết hạn sớm nhất trong các lô còn hàng (RemainingQuantity > 0).
    /// Null nếu không có lô nào có ExpiredDate hoặc ItemType = Reusable.
    /// </summary>
    public DateTime? NearestExpiryDate { get; set; }

    /// <summary>
    /// Số lô đang còn hàng (RemainingQuantity > 0). 0 nếu ItemType = Reusable.
    /// </summary>
    public int LotCount { get; set; }
}
