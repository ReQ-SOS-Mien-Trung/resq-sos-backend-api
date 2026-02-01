using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Entities.Logistics;

namespace RESQ.Infrastructure.Entities.Finance;

[Table("depot_fund_allocations")]
public partial class DepotFundAllocation
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("fund_campaign_id")]
    public int? FundCampaignId { get; set; }

    [Column("depot_id")]
    public int? DepotId { get; set; }

    [Column("amount")]
    public decimal? Amount { get; set; }

    [Column("purpose")]
    public string? Purpose { get; set; }

    [Column("status")]
    [StringLength(50)]
    public string? Status { get; set; }

    [Column("allocated_by")]
    public Guid? AllocatedBy { get; set; }

    [Column("allocated_at", TypeName = "timestamp with time zone")]
    public DateTime? AllocatedAt { get; set; }

    [ForeignKey("AllocatedBy")]
    [InverseProperty("DepotFundAllocations")] // Assuming User has this navigation, though not explicitly in previous User code, diagram implies link.
    public virtual User? AllocatedByUser { get; set; }

    [ForeignKey("DepotId")]
    [InverseProperty("DepotFundAllocations")]
    public virtual Depot? Depot { get; set; }

    [ForeignKey("FundCampaignId")]
    [InverseProperty("DepotFundAllocations")]
    public virtual FundCampaign? FundCampaign { get; set; }
}
