using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Logistics;

[Table("depot_supply_request_reusable_items")]
public class DepotSupplyRequestReusableItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("supply_request_id")]
    public int SupplyRequestId { get; set; }

    [Column("reusable_item_id")]
    public int ReusableItemId { get; set; }

    [Column("status")]
    [StringLength(30)]
    public string Status { get; set; } = "Reserved";

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at", TypeName = "timestamp with time zone")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(SupplyRequestId))]
    [InverseProperty(nameof(DepotSupplyRequest.ReusableItems))]
    public virtual DepotSupplyRequest SupplyRequest { get; set; } = null!;

    [ForeignKey(nameof(ReusableItemId))]
    [InverseProperty(nameof(ReusableItem.SupplyRequestItems))]
    public virtual ReusableItem ReusableItem { get; set; } = null!;
}
