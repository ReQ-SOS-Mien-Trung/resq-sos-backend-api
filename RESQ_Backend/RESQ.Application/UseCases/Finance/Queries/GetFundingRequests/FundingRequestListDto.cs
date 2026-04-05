namespace RESQ.Application.UseCases.Finance.Queries.GetFundingRequests;

public class FundingRequestListDto
{
    public int Id { get; set; }
    public int DepotId { get; set; }
    public string DepotName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? ApprovedCampaignId { get; set; }
    public string? ApprovedCampaignName { get; set; }
    public string? RequestedByUserName { get; set; }
    public string? ReviewedByUserName { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public class FundingRequestItemListDto
{
    public int Id { get; set; }
    public int Row { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public string TargetGroup { get; set; } = string.Empty;
    public DateOnly? ReceivedDate { get; set; }
    public DateOnly? ExpiredDate { get; set; }
    public string? Notes { get; set; }
}
