namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryByCategory;

public class DepotCategoryQuantityDto
{
    public int CategoryId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>Tổng số lượng vật phẩm tiêu hao (Consumable) trong kho - đếm theo số lượng (gói, chai, viên...). Lấy từ bảng supply_inventory.</summary>
    public int TotalConsumableQuantity { get; set; }

    /// <summary>Số lượng vật phẩm tiêu hao còn khả dụng (Quantity - ReservedQuantity). Lấy từ bảng supply_inventory.</summary>
    public int AvailableConsumableQuantity { get; set; }

    /// <summary>Tổng số đơn vị vật phẩm tái sử dụng (mọi trạng thái).</summary>
    public int TotalReusableUnits { get; set; }

    /// <summary>Số đơn vị vật phẩm tái sử dụng đang sẵn sàng (Available).</summary>
    public int AvailableReusableUnits { get; set; }
}

