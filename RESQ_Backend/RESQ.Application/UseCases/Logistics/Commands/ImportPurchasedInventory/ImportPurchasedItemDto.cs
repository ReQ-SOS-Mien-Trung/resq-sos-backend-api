namespace RESQ.Application.UseCases.Logistics.Commands.ImportPurchasedInventory;

public class ImportPurchasedItemDto
{
    public int Row { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public string TargetGroup { get; set; } = string.Empty;
    public DateOnly? ReceivedDate { get; set; }
    public DateOnly? ExpiredDate { get; set; }
    public string Notes { get; set; } = string.Empty;
}
