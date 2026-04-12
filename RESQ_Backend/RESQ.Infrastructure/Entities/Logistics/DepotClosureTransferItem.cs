using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Logistics;

[Table("depot_closure_transfer_items")]
public class DepotClosureTransferItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("transfer_id")]
    public int TransferId { get; set; }

    [Column("item_model_id")]
    public int ItemModelId { get; set; }

    [Column("item_name")]
    public string ItemName { get; set; } = string.Empty;

    [Column("item_type")]
    [StringLength(50)]
    public string ItemType { get; set; } = string.Empty;

    [Column("unit")]
    [StringLength(50)]
    public string? Unit { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }

    [ForeignKey(nameof(TransferId))]
    public DepotClosureTransfer? Transfer { get; set; }
}
