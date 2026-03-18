using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Finance;

[Table("funding_request_items")]
public partial class FundingRequestItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("funding_request_id")]
    public int FundingRequestId { get; set; }

    [Column("row")]
    public int Row { get; set; }

    [Column("item_name")]
    [StringLength(255)]
    public string ItemName { get; set; } = string.Empty;

    [Column("category_code")]
    [StringLength(50)]
    public string CategoryCode { get; set; } = string.Empty;

    [Column("unit")]
    [StringLength(50)]
    public string? Unit { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("unit_price")]
    public decimal UnitPrice { get; set; }

    [Column("total_price")]
    public decimal TotalPrice { get; set; }

    [Column("item_type")]
    [StringLength(50)]
    public string ItemType { get; set; } = string.Empty;

    [Column("target_group")]
    [StringLength(100)]
    public string TargetGroup { get; set; } = string.Empty;

    [Column("received_date")]
    public DateOnly? ReceivedDate { get; set; }

    [Column("expired_date")]
    public DateOnly? ExpiredDate { get; set; }

    [Column("notes")]
    [StringLength(500)]
    public string? Notes { get; set; }

    [ForeignKey("FundingRequestId")]
    [InverseProperty("FundingRequestItems")]
    public virtual FundingRequest FundingRequest { get; set; } = null!;
}
