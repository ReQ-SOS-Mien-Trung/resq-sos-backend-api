namespace RESQ.Application.UseCases.Finance.Queries.GetPublicCampaignSpending;

/// <summary>
/// Public campaign spending overview grouped by depot for donors/contributors.
/// </summary>
public class PublicCampaignSpendingDto
{
    public int CampaignId { get; set; }
    public string CampaignName { get; set; } = string.Empty;
    public decimal TotalRaised { get; set; }
    public decimal TotalDisbursed { get; set; }
    public decimal RemainingBalance { get; set; }
    public List<PublicDepotCampaignSpendingDto> Depots { get; set; } = [];
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}

public class PublicDepotCampaignSpendingDto
{
    public int DepotId { get; set; }
    public string? DepotName { get; set; }
    /// <summary>Total campaign money allocated to this depot.</summary>
    public decimal TotalAllocated { get; set; }
    public List<PublicDepotCampaignAllocationDto> Allocations { get; set; } = [];
    /// <summary>Total reported purchase amount from this campaign at this depot.</summary>
    public decimal TotalSpent { get; set; }
    public List<PublicDepotCampaignImportDto> Imports { get; set; } = [];
}

public class PublicDepotCampaignAllocationDto
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string? Purpose { get; set; }
    public string Type { get; set; } = string.Empty;
    public int? FundingRequestId { get; set; }
    /// <summary>Allocation time displayed in Vietnam time (UTC+7).</summary>
    public DateTime AllocatedAt { get; set; }
}

public class PublicDepotCampaignImportDto
{
    public int? VatInvoiceId { get; set; }
    public int DepotFundId { get; set; }
    public string? InvoiceSerial { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? SupplierName { get; set; }
    public DateOnly? InvoiceDate { get; set; }
    public decimal? InvoiceTotalAmount { get; set; }
    public DateTime? ImportedAt { get; set; }
    /// <summary>Total imported item amount in this import batch.</summary>
    public decimal TotalSpent { get; set; }
    public List<PublicDepotCampaignPurchasedItemDto> Items { get; set; } = [];
}

public class PublicDepotCampaignPurchasedItemDto
{
    public string ItemName { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public DateTime? ExpiredDate { get; set; }
    public string ItemType { get; set; } = string.Empty;
}
