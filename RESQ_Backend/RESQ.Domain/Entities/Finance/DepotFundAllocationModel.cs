using System;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Domain.Entities.Finance;

public class DepotFundAllocationModel
{
    public int Id { get; set; }
    public int? FundCampaignId { get; set; }
    public int? DepotId { get; set; }
    public decimal? Amount { get; set; }
    public string? Purpose { get; set; }
    public string? Status { get; set; } // Keeping as string for now as it wasn't specified to be an Enum, or we can assume active/completed
    public Guid? AllocatedBy { get; set; }
    public DateTime? AllocatedAt { get; set; }
    
    // View properties
    public string? FundCampaignName { get; set; }
    public string? DepotName { get; set; }
}
