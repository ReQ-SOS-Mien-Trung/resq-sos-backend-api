namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotInventory;

public class InventoryItemDto
{
    public int ReliefItemId { get; set; }
    public string ReliefItemName { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? ItemType { get; set; }
    public string? TargetGroup { get; set; }
    public int Quantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public DateTime? LastStockedAt { get; set; }
}