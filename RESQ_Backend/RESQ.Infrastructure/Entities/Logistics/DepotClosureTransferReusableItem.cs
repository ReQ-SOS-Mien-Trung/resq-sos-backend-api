using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Logistics;

[Table("depot_closure_transfer_reusable_items")]
public class DepotClosureTransferReusableItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("transfer_id")]
    public int TransferId { get; set; }

    [Column("reusable_item_id")]
    public int ReusableItemId { get; set; }

    [Column("status")]
    [StringLength(30)]
    public string Status { get; set; } = "Reserved";

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp with time zone")]
    public DateTime UpdatedAt { get; set; }

    [ForeignKey(nameof(TransferId))]
    public DepotClosureTransfer? Transfer { get; set; }

    [ForeignKey(nameof(ReusableItemId))]
    public ReusableItem? ReusableItem { get; set; }
}
