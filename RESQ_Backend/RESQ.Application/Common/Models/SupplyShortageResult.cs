namespace RESQ.Application.Common.Models;

/// <summary>K?t qu? tr? v? khi m?t v?t ph?m không d? t?n kho ho?c không có trong kho.</summary>
public class SupplyShortageResult
{
    public int ItemModelId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int RequestedQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public bool NotFound { get; set; }
}
