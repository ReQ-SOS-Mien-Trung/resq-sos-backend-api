using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Entities;

[Table("donations")]
public partial class Donation
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("fund_campaign_id")]
    public int? FundCampaignId { get; set; }

    [Column("donor_name")]
    [StringLength(255)]
    public string? DonorName { get; set; }

    [Column("donor_phone")]
    [StringLength(20)]
    public string? DonorPhone { get; set; }

    [Column("donor_email")]
    [StringLength(255)]
    public string? DonorEmail { get; set; }

    [Column("amount")]
    public decimal? Amount { get; set; }

    [Column("payos_order_id")]
    [StringLength(100)]
    public string? PayosOrderId { get; set; }

    [Column("payos_transaction_id")]
    [StringLength(100)]
    public string? PayosTransactionId { get; set; }

    [Column("payos_status")]
    [StringLength(50)]
    public string? PayosStatus { get; set; }

    [Column("paid_at", TypeName = "timestamp with time zone")]
    public DateTime? PaidAt { get; set; }

    [Column("note")]
    public string? Note { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("FundCampaignId")]
    [InverseProperty("Donations")]
    public virtual FundCampaign? FundCampaign { get; set; }
}