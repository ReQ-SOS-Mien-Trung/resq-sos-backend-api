namespace RESQ.Application.UseCases.Finance.Queries.GetCampaignDisbursements;

public class CampaignDisbursementListDto
{
    public int Id { get; set; }
    public int FundCampaignId { get; set; }
    public string? FundCampaignName { get; set; }
    public int DepotId { get; set; }
    public string? DepotName { get; set; }
    public decimal Amount { get; set; }
    public string? Purpose { get; set; }
    public string Type { get; set; } = string.Empty;
    public int? FundingRequestId { get; set; }
    public string? CreatedByUserName { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<DisbursementItemListDto> Items { get; set; } = [];
}

public class DisbursementItemListDto
{
    public int Id { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Note { get; set; }
}
