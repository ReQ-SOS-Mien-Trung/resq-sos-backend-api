using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Domain.Entities.Logistics;

public class InventoryItemModel
{
    public int ReliefItemId { get; set; }
    public string ReliefItemName { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? ItemType { get; set; }
    public string? TargetGroup { get; set; }
    public InventoryAvailability Availability { get; set; } = default!;
    public DateTime? LastStockedAt { get; set; }
}