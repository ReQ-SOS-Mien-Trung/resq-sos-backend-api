using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Entities.Logistics;

namespace RESQ.Infrastructure.Entities.Finance;

[Table("funding_requests")]
public partial class FundingRequest
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("depot_id")]
    public int DepotId { get; set; }

    [Column("requested_by")]
    public Guid RequestedBy { get; set; }

    [Column("total_amount")]
    public decimal TotalAmount { get; set; }

    [Column("description")]
    [StringLength(2000)]
    public string? Description { get; set; }

    [Column("attachment_url")]
    [StringLength(500)]
    public string? AttachmentUrl { get; set; }

    [Column("status")]
    [StringLength(50)]
    public string Status { get; set; } = string.Empty;

    [Column("approved_campaign_id")]
    public int? ApprovedCampaignId { get; set; }

    [Column("reviewed_by")]
    public Guid? ReviewedBy { get; set; }

    [Column("reviewed_at", TypeName = "timestamp with time zone")]
    public DateTime? ReviewedAt { get; set; }

    [Column("rejection_reason")]
    [StringLength(1000)]
    public string? RejectionReason { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("DepotId")]
    [InverseProperty("FundingRequests")]
    public virtual Depot Depot { get; set; } = null!;

    [ForeignKey("RequestedBy")]
    [InverseProperty("FundingRequestsCreated")]
    public virtual User RequestedByUser { get; set; } = null!;

    [ForeignKey("ApprovedCampaignId")]
    [InverseProperty("FundingRequests")]
    public virtual FundCampaign? ApprovedCampaign { get; set; }

    [ForeignKey("ReviewedBy")]
    [InverseProperty("FundingRequestsReviewed")]
    public virtual User? ReviewedByUser { get; set; }

    [InverseProperty("FundingRequest")]
    public virtual ICollection<FundingRequestItem> FundingRequestItems { get; set; } = new List<FundingRequestItem>();

    [InverseProperty("FundingRequest")]
    public virtual CampaignDisbursement? CampaignDisbursement { get; set; }
}
