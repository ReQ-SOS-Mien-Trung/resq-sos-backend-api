namespace RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems;

public class LowStockRawItemDto
{
    public int DepotId { get; set; }
    public string DepotName { get; set; } = string.Empty;

    public int ItemModelId { get; set; }
    public string ItemModelName { get; set; } = string.Empty;
    public string? Unit { get; set; }

    public int? CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? TargetGroup { get; set; }

    public int Quantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int AvailableQuantity { get; set; }
}
