namespace RESQ.Application.UseCases.Logistics.Queries.SearchWarehousesByItems;

/// <summary>
/// Groups all depot availability records for a single relief item.
/// </summary>
public class ItemWarehouseAvailabilityDto
{
    public int ReliefItemId { get; set; }
    public string ReliefItemName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? ItemType { get; set; }
    public string? Unit { get; set; }
    public int TotalAvailableAcrossWarehouses { get; set; }
    public List<WarehouseStockDto> Warehouses { get; set; } = new();
}
