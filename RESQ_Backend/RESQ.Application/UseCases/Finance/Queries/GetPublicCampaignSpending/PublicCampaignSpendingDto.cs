namespace RESQ.Application.UseCases.Finance.Queries.GetPublicCampaignSpending;

/// <summary>
/// Tổng quan minh bạch chi tiêu campaign - công khai cho donor.
/// </summary>
public class PublicCampaignSpendingDto
{
    public int CampaignId { get; set; }
    public string CampaignName { get; set; } = string.Empty;
    public decimal TotalRaised { get; set; }
    public decimal TotalDisbursed { get; set; }
    public decimal RemainingBalance { get; set; }
    public List<PublicDisbursementDto> Disbursements { get; set; } = [];
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}

public class PublicDisbursementDto
{
    public int Id { get; set; }
    public int DepotId { get; set; }
    public string? DepotName { get; set; }
    public decimal Amount { get; set; }
    public string? Purpose { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    
    /// <summary>Số dư quỹ kho hiện tại của depot liên quan.</summary>
    public decimal DepotFundBalance { get; set; }
    
    public List<PublicDisbursementItemDto> Items { get; set; } = [];
}

public class PublicDisbursementItemDto
{
    public string ItemName { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}
