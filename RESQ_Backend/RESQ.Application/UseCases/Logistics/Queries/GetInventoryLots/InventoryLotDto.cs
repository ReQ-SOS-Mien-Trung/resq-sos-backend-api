namespace RESQ.Application.UseCases.Logistics.Queries.GetInventoryLots;

public class InventoryLotDto
{
    public int LotId { get; set; }
    public int Quantity { get; set; }
    public int RemainingQuantity { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public DateTime? ExpiredDate { get; set; }
    public string? SourceType { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsExpiringSoon { get; set; }
    public bool IsExpired { get; set; }
}
