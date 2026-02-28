using System;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Domain.Entities.Finance;

public class FundTransactionModel
{
    public int Id { get; set; }
    public int? FundCampaignId { get; set; }
    
    public TransactionType Type { get; set; }
    
    public string? Direction { get; set; }
    public decimal? Amount { get; set; }
    
    public TransactionReferenceType ReferenceType { get; set; }
    
    public int? ReferenceId { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? CreatedAt { get; set; }
    
    public string? FundCampaignName { get; set; }
    public string? CreatedByUserName { get; set; }
}
