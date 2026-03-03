using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Entities.Finance;

[Table("fund_campaigns")]
public partial class FundCampaign
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("code")]
    [StringLength(50)]
    public string? Code { get; set; }

    [Column("name")]
    [StringLength(255)]
    public string? Name { get; set; }

    [Column("region")]
    [StringLength(255)]
    public string? Region { get; set; }

    [Column("campaign_start_date")]
    public DateOnly? CampaignStartDate { get; set; }

    [Column("campaign_end_date")]
    public DateOnly? CampaignEndDate { get; set; }

    [Column("target_amount")]
    public decimal? TargetAmount { get; set; }

    [Column("total_amount")]
    public decimal? TotalAmount { get; set; }

    [Column("status")]
    [StringLength(50)]
    public string? Status { get; set; }

    [Column("created_by")]
    public Guid? CreatedBy { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("last_modified_by")]
    public Guid? LastModifiedBy { get; set; }

    [Column("last_modified_at", TypeName = "timestamp with time zone")]
    public DateTime? LastModifiedAt { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;

    [ForeignKey("CreatedBy")]
    [InverseProperty("FundCampaigns")]
    public virtual User? CreatedByUser { get; set; }

    [InverseProperty("FundCampaign")]
    public virtual ICollection<DepotFundAllocation> DepotFundAllocations { get; set; } = new List<DepotFundAllocation>();

    [InverseProperty("FundCampaign")]
    public virtual ICollection<Donation> Donations { get; set; } = new List<Donation>();

    [InverseProperty("FundCampaign")]
    public virtual ICollection<FundTransaction> FundTransactions { get; set; } = new List<FundTransaction>();
}
