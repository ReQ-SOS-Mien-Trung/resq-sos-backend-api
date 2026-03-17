namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotInventory;

public class InventoryItemDto
{
    public int ItemModelId { get; set; }
    public string ItemModelName { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? ItemType { get; set; }
    public string? TargetGroup { get; set; }

    /// <summary>Tổng số lượng (tiêu hao) hoặc tổng số đơn vị (tái sử dụng).</summary>
    public int Quantity { get; set; }

    /// <summary>Số lượng đang được giữ/dùng (tiêu hao) hoặc InUse+Maintenance (tái sử dụng).</summary>
    public int ReservedQuantity { get; set; }

    /// <summary>Số lượng/đơn vị khả dụng.</summary>
    public int AvailableQuantity { get; set; }

    public DateTime? LastStockedAt { get; set; }

    /// <summary>
    /// Chi tiết trạng thái và tình trạng cho vật phẩm tái sử dụng.
    /// Null với ItemType = Consumable.
    /// </summary>
    public ReusableBreakdownDto? ReusableBreakdown { get; set; }
}

/// <summary>Chi tiết thống kê trạng thái + tình trạng vật phẩm tái sử dụng.</summary>
public class ReusableBreakdownDto
{
    public int TotalUnits { get; set; }
    public int AvailableUnits { get; set; }
    public int InUseUnits { get; set; }
    public int MaintenanceUnits { get; set; }
    public int DecommissionedUnits { get; set; }
    public int GoodCount { get; set; }
    public int FairCount { get; set; }
    public int PoorCount { get; set; }
}

