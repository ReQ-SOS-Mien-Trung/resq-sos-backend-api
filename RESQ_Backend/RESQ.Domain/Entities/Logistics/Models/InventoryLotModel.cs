namespace RESQ.Domain.Entities.Logistics.Models;

public class InventoryLotModel
{
    public int Id { get; set; }
    public int SupplyInventoryId { get; set; }
    public int Quantity { get; set; }
    public int RemainingQuantity { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public DateTime? ExpiredDate { get; set; }
    public string? SourceType { get; set; }
    public int? SourceId { get; set; }
    public DateTime CreatedAt { get; set; }
}
