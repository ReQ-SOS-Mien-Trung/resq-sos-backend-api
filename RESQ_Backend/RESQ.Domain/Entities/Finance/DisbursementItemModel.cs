namespace RESQ.Domain.Entities.Finance;

/// <summary>
/// Chi ti?t v?t ph?m dã mua t? ti?n gi?i ngân - công khai cho donor xem.
/// </summary>
public class DisbursementItemModel
{
    public int Id { get; set; }
    public int CampaignDisbursementId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Note { get; set; }
    public DateTime? CreatedAt { get; set; }
}
