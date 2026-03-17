using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Logistics;

[Table("supply_inventory")]
public partial class SupplyInventory
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("depot_id")]
    public int? DepotId { get; set; }

    [Column("item_model_id")]
    public int? ItemModelId { get; set; }

    [Column("quantity")]
    public int? Quantity { get; set; }

    [Column("reserved_quantity")]
    public int? ReservedQuantity { get; set; }

    [Column("last_stocked_at", TypeName = "timestamp with time zone")]
    public DateTime? LastStockedAt { get; set; }

    [ForeignKey("DepotId")]
    [InverseProperty("SupplyInventories")]
    public virtual Depot? Depot { get; set; }

    [ForeignKey("ItemModelId")]
    [InverseProperty("SupplyInventories")]
    public virtual ItemModel? ItemModel { get; set; }

    [InverseProperty("SupplyInventory")]
    public virtual ICollection<InventoryLog> InventoryLogs { get; set; } = new List<InventoryLog>();
}
