using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Finance;

[Table("disbursement_items")]
public partial class DisbursementItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("campaign_disbursement_id")]
    public int CampaignDisbursementId { get; set; }

    [Column("item_name")]
    [StringLength(255)]
    public string ItemName { get; set; } = string.Empty;

    [Column("unit")]
    [StringLength(50)]
    public string? Unit { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("unit_price")]
    public decimal UnitPrice { get; set; }

    [Column("total_price")]
    public decimal TotalPrice { get; set; }

    [Column("note")]
    [StringLength(500)]
    public string? Note { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("CampaignDisbursementId")]
    [InverseProperty("DisbursementItems")]
    public virtual CampaignDisbursement CampaignDisbursement { get; set; } = null!;
}
