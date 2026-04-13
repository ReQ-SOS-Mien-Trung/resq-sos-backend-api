namespace RESQ.Application.Common.Models;

/// <summary>Kết quả trả về khi một vật phẩm không đủ tồn kho hoặc không có trong kho.</summary>
public class SupplyShortageResult
{
    public int ItemModelId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int RequestedQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public bool NotFound { get; set; }
}
