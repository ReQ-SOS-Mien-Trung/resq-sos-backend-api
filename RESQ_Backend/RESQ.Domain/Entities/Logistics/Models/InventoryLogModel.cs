namespace RESQ.Domain.Entities.Logistics.Models;

public class InventoryLogModel
{
    public int Id { get; set; }
    public int? DepotSupplyInventoryId { get; set; }
    public int? SupplyInventoryLotId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public int? QuantityChange { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public int? SourceId { get; set; }
    public string? Note { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public DateTime? ExpiredDate { get; set; }
    public string? PerformedByName { get; set; }
    public int? DepotId { get; set; }
    public string? DepotName { get; set; }
    public int? ItemModelId { get; set; }
    public string? ItemModelName { get; set; }
}