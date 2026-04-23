using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Logistics;

[Table("depot_supply_request_consumable_reservations")]
public class DepotSupplyRequestConsumableReservation
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("supply_request_id")]
    public int SupplyRequestId { get; set; }

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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at", TypeName = "timestamp with time zone")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(SupplyRequestId))]
    [InverseProperty(nameof(DepotSupplyRequest.ConsumableReservations))]
    public virtual DepotSupplyRequest SupplyRequest { get; set; } = null!;

    [ForeignKey(nameof(SupplyInventoryId))]
    [InverseProperty(nameof(SupplyInventory.SupplyRequestReservations))]
    public virtual SupplyInventory SupplyInventory { get; set; } = null!;

    [ForeignKey(nameof(SupplyInventoryLotId))]
    [InverseProperty(nameof(SupplyInventoryLot.SupplyRequestReservations))]
    public virtual SupplyInventoryLot? SupplyInventoryLot { get; set; }
}
