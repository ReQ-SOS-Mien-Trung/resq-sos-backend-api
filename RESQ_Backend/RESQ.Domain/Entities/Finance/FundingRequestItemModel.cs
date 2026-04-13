namespace RESQ.Domain.Entities.Finance;

/// <summary>
/// Chi tiết từng dòng vật tư trong FundingRequest - cùng cấu trúc với ImportPurchasedItemDto.
/// </summary>
public class FundingRequestItemModel
{
    public int Id { get; set; }
    public int FundingRequestId { get; set; }
    public int Row { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public List<string> TargetGroups { get; set; } = new();
    public decimal VolumePerUnit { get; set; }
    public decimal WeightPerUnit { get; set; }
    public DateOnly? ReceivedDate { get; set; }
    public DateOnly? ExpiredDate { get; set; }
    public string? Notes { get; set; }
    public string? ImageUrl { get; set; }
}
