using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Entities.Logistics;

[Table("depot_supply_inventory")]
public partial class DepotSupplyInventory
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("depot_id")]
    public int? DepotId { get; set; }

    [Column("relief_item_id")]
    public int? ReliefItemId { get; set; }

    [Column("quantity")]
    public int? Quantity { get; set; }

    [Column("reserved_quantity")]
    public int? ReservedQuantity { get; set; }

    [Column("last_stocked_at", TypeName = "timestamp with time zone")]
    public DateTime? LastStockedAt { get; set; }

    [ForeignKey("DepotId")]
    [InverseProperty("DepotSupplyInventories")]
    public virtual Depot? Depot { get; set; }

    [ForeignKey("ReliefItemId")]
    [InverseProperty("DepotSupplyInventories")]
    public virtual ReliefItem? ReliefItem { get; set; }

    [InverseProperty("DepotSupplyInventory")]
    public virtual ICollection<InventoryLog> InventoryLogs { get; set; } = new List<InventoryLog>();
}