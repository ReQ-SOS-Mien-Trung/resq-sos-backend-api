using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Entities.Finance;

[Table("fund_transactions")]
public partial class FundTransaction
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("fund_campaign_id")]
    public int? FundCampaignId { get; set; }

    [Column("type")]
    [StringLength(50)]
    public string? Type { get; set; }

    [Column("direction")]
    [StringLength(50)]
    public string? Direction { get; set; }

    [Column("amount")]
    public decimal? Amount { get; set; }

    [Column("reference_type")]
    [StringLength(50)]
    public string? ReferenceType { get; set; }

    [Column("reference_id")]
    public int? ReferenceId { get; set; }

    [Column("created_by")]
    public Guid? CreatedBy { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("CreatedBy")]
    [InverseProperty("FundTransactions")]
    public virtual User? CreatedByUser { get; set; }

    [ForeignKey("FundCampaignId")]
    [InverseProperty("FundTransactions")]
    public virtual FundCampaign? FundCampaign { get; set; }
}