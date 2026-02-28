using System;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Domain.Entities.Finance;

public class FundCampaignModel
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Region { get; set; }
    public DateOnly? CampaignStartDate { get; set; }
    public DateOnly? CampaignEndDate { get; set; }
    public decimal? TargetAmount { get; set; }
    public decimal? TotalAmount { get; set; }
    
    public FundCampaignStatus Status { get; set; }
    
    public Guid? CreatedBy { get; set; }
    public DateTime? CreatedAt { get; set; }
}
