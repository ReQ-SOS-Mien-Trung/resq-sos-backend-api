using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Logistics;

[Table("depot_closure_transfer_consumable_reservations")]
public class DepotClosureTransferConsumableReservation
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("transfer_id")]
    public int TransferId { get; set; }

    [Column("supply_inventory_id")]
    public int SupplyInventoryId { get; set; }

    [Column("supply_inventory_lot_id")]
    public int? SupplyInventoryLotId { get; set; }

    [Column("item_model_id")]
    public int ItemModelId { get; set; }

    [Column("reserved_quantity")]
    public int ReservedQuantity { get; set; }

    [Column("status")]
    [StringLength(30)]
    public string Status { get; set; } = "Reserved";

    [Column("received_date", TypeName = "timestamp with time zone")]
    public DateTime? ReceivedDate { get; set; }

    [Column("expired_date", TypeName = "timestamp with time zone")]
    public DateTime? ExpiredDate { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp with time zone")]
    public DateTime UpdatedAt { get; set; }

    [ForeignKey(nameof(TransferId))]
    public DepotClosureTransfer? Transfer { get; set; }

    [ForeignKey(nameof(SupplyInventoryId))]
    public SupplyInventory? SupplyInventory { get; set; }

    [ForeignKey(nameof(SupplyInventoryLotId))]
    public SupplyInventoryLot? SupplyInventoryLot { get; set; }
}
