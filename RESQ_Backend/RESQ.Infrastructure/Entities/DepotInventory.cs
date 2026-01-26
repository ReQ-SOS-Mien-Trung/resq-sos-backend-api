using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities;

namespace RESQ.Infrastructure.Entities;

[Table("depot_inventory")]
public partial class DepotInventory
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

    [Column("last_stocked_at", TypeName = "timestamp without time zone")]
    public DateTime? LastStockedAt { get; set; }

    [ForeignKey("DepotId")]
    [InverseProperty("DepotInventories")]
    public virtual Depot? Depot { get; set; }

    [InverseProperty("DepotInventory")]
    public virtual ICollection<InventoryLog> InventoryLogs { get; set; } = new List<InventoryLog>();

    [ForeignKey("ReliefItemId")]
    [InverseProperty("DepotInventories")]
    public virtual ReliefItem? ReliefItem { get; set; }
}
