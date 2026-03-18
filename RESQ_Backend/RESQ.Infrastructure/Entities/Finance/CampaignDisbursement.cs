using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Entities.Logistics;

namespace RESQ.Infrastructure.Entities.Finance;

[Table("campaign_disbursements")]
public partial class CampaignDisbursement
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("fund_campaign_id")]
    public int FundCampaignId { get; set; }

    [Column("depot_id")]
    public int DepotId { get; set; }

    [Column("amount")]
    public decimal Amount { get; set; }

    [Column("purpose")]
    [StringLength(1000)]
    public string? Purpose { get; set; }

    [Column("type")]
    [StringLength(50)]
    public string Type { get; set; } = string.Empty;

    [Column("funding_request_id")]
    public int? FundingRequestId { get; set; }

    [Column("created_by")]
    public Guid CreatedBy { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("FundCampaignId")]
    [InverseProperty("CampaignDisbursements")]
    public virtual FundCampaign FundCampaign { get; set; } = null!;

    [ForeignKey("DepotId")]
    [InverseProperty("CampaignDisbursements")]
    public virtual Depot Depot { get; set; } = null!;

    [ForeignKey("FundingRequestId")]
    [InverseProperty("CampaignDisbursement")]
    public virtual FundingRequest? FundingRequest { get; set; }

    [ForeignKey("CreatedBy")]
    [InverseProperty("CampaignDisbursements")]
    public virtual User CreatedByUser { get; set; } = null!;

    [InverseProperty("CampaignDisbursement")]
    public virtual ICollection<DisbursementItem> DisbursementItems { get; set; } = new List<DisbursementItem>();
}
