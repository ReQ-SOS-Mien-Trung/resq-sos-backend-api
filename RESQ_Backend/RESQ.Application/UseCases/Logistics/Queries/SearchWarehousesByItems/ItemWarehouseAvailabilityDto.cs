namespace RESQ.Application.UseCases.Logistics.Queries.SearchWarehousesByItems;

/// <summary>
/// Groups all depot availability records for a single relief item.
/// </summary>
public class ItemWarehouseAvailabilityDto
{
    public int ItemModelId { get; set; }
    public string ItemModelName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? ItemType { get; set; }
    public string? Unit { get; set; }
    public int TotalAvailableAcrossWarehouses { get; set; }
    public List<WarehouseStockDto> Warehouses { get; set; } = new();
}
