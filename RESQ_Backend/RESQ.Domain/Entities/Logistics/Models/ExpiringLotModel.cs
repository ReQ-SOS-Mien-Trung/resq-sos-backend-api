namespace RESQ.Domain.Entities.Logistics.Models;

public class ExpiringLotModel
{
    public int LotId { get; set; }
    public int SupplyInventoryId { get; set; }
    public int ItemModelId { get; set; }
    public string? ItemModelName { get; set; }
    public int RemainingQuantity { get; set; }
    public DateTime? ExpiredDate { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public string? SourceType { get; set; }
    public bool IsExpired { get; set; }
}
