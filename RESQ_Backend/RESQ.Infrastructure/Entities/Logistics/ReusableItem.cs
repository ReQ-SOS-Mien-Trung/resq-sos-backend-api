using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Logistics;

[Table("reusable_items")]
public partial class ReusableItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("depot_id")]
    public int? DepotId { get; set; }

    [Column("item_model_id")]
    public int? ItemModelId { get; set; }

    [Column("serial_number")]
    [StringLength(100)]
    public string? SerialNumber { get; set; }

    [Column("status")]
    [StringLength(50)]
    public string? Status { get; set; }

    [Column("condition")]
    [StringLength(50)]
    public string? Condition { get; set; }

    /// <summary>
    /// FK linking this unit to the supply request that has reserved or is currently transferring it.
    /// Null when the item is not part of any active transfer.
    /// </summary>
    [Column("supply_request_id")]
    public int? SupplyRequestId { get; set; }

    [Column("note")]
    public string? Note { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp with time zone")]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey("DepotId")]
    [InverseProperty("ReusableItems")]
    public virtual Depot? Depot { get; set; }

    [ForeignKey("ItemModelId")]
    [InverseProperty("ReusableItems")]
    public virtual ItemModel? ItemModel { get; set; }
}
